using ExchangeRateSync.Data;
using ExchangeRateSync.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ExchangeRateSync.Services;

public class ExchangeRateSyncWorker(
    IServiceScopeFactory scopeFactory,
    IOptions<SyncOptions> options,
    ILogger<ExchangeRateSyncWorker> logger) : BackgroundService
{
    private readonly TimeOnly _runTimeUtc = options.Value.DailyRunTimeUtc;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await MarkInterruptedRunsAsFailedAsync(stoppingToken);
            if (await IsCatchUpNeededAsync(stoppingToken))
            {
                logger.LogInformation("No successful sync since the last scheduled run. Running catch-up sync.");
                await RunSyncAsync(stoppingToken);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Catch-up check failed.");
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            var nextRunUtc = GetNextRunUtc(DateTime.UtcNow);
            logger.LogInformation("Next scheduled sync at {NextRunUtc:u}.", nextRunUtc);

            var delay = nextRunUtc - DateTime.UtcNow;
            if (delay > TimeSpan.Zero)
            {
                try
                {
                    await Task.Delay(delay, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }

            await RunSyncAsync(stoppingToken);
        }
    }

    private async Task RunSyncAsync(CancellationToken stoppingToken)
    {
        try
        {
            // The sync service and DbContext are scoped, so each run gets its own scope.
            await using var scope = scopeFactory.CreateAsyncScope();
            var syncService = scope.ServiceProvider.GetRequiredService<ExchangeRateSyncService>();

            await syncService.RunAsync(SyncTrigger.Scheduled, stoppingToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Never let an exception stop the worker; the next scheduled run will try again.
            logger.LogError(ex, "Scheduled sync could not be completed.");
        }
    }

    private async Task MarkInterruptedRunsAsFailedAsync(CancellationToken stoppingToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // The sync lock lives in memory, so nothing can be running when the application starts.
        // Any "Running" entry left in the database was interrupted by a previous shutdown.
        var interruptedCount = await dbContext.SyncLogs
            .Where(l => l.Status == SyncStatus.Running)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(l => l.Status, SyncStatus.Failed)
                .SetProperty(l => l.ErrorMessage, "Interrupted: the application stopped before the sync finished."),
                stoppingToken);

        if (interruptedCount > 0)
        {
            logger.LogWarning("Marked {Count} interrupted sync run(s) as failed.", interruptedCount);
        }
    }


    private async Task<bool> IsCatchUpNeededAsync(CancellationToken stoppingToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var lastScheduledRunUtc = GetLastRunUtc(DateTime.UtcNow);

        var hasSucceededSinceLastRun = await dbContext.SyncLogs.AnyAsync(
            l => l.Status == SyncStatus.Succeeded && l.StartedAtUtc >= lastScheduledRunUtc,
            stoppingToken);

        return !hasSucceededSinceLastRun;
    }

    private DateTime GetNextRunUtc(DateTime nowUtc)
    {
        var todayRunUtc = nowUtc.Date + _runTimeUtc.ToTimeSpan();
        return todayRunUtc > nowUtc ? todayRunUtc : todayRunUtc.AddDays(1);
    }

    private DateTime GetLastRunUtc(DateTime nowUtc)
    {
        var todayRunUtc = nowUtc.Date + _runTimeUtc.ToTimeSpan();
        return todayRunUtc <= nowUtc ? todayRunUtc : todayRunUtc.AddDays(-1);
    }
}