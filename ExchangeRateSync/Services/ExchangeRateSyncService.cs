using System.Globalization;
using ExchangeRateSync.Clients;
using ExchangeRateSync.Data;
using ExchangeRateSync.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ExchangeRateSync.Services;

public class ExchangeRateSyncService(
    AppDbContext dbContext,
    FrankfurterClient frankfurterClient,
    IOptions<SyncOptions> options,
    ILogger<ExchangeRateSyncService> logger)
{
    private const int MaxErrorMessageLength = 2000;
    private const int RatePrecision = 6;

    // Shared across all scopes so a manual sync and the scheduled sync never run at the same time.
    private static readonly SemaphoreSlim SyncLock = new(1, 1);

    private readonly SyncOptions _options = options.Value;

    public async Task<SyncLog> RunAsync(SyncTrigger trigger, CancellationToken cancellationToken = default)
    {
        if (!await SyncLock.WaitAsync(0, cancellationToken))
        {
            throw new InvalidOperationException("A synchronization is already running.");
        }

        try
        {
            return await RunInternalAsync(trigger, cancellationToken);
        }
        finally
        {
            SyncLock.Release();
        }
    }

    private async Task<SyncLog> RunInternalAsync(SyncTrigger trigger, CancellationToken cancellationToken)
    {
        var syncLog = new SyncLog
        {
            StartedAtUtc = DateTime.UtcNow,
            Status = SyncStatus.Running,
            Trigger = trigger
        };

        dbContext.SyncLogs.Add(syncLog);
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Exchange rate sync {SyncLogId} started ({Trigger}).", syncLog.Id, trigger);

        try
        {
            var (startDate, endDate) = await GetDateRangeAsync(cancellationToken);
            var response = await FetchWithRetryAsync(startDate, endDate, syncLog, cancellationToken);
            await UpsertRatesAsync(response, syncLog, cancellationToken);

            syncLog.Status = SyncStatus.Succeeded;
            syncLog.FinishedAtUtc = DateTime.UtcNow;

            // Rates and the log entry are saved in a single transaction.
            await dbContext.SaveChangesAsync(cancellationToken);

            logger.LogInformation(
                "Exchange rate sync {SyncLogId} succeeded. Inserted: {Inserted}, updated: {Updated}.",
                syncLog.Id, syncLog.InsertedCount, syncLog.UpdatedCount);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Exchange rate sync {SyncLogId} failed.", syncLog.Id);

            // Discard any pending rate changes so only the failed log entry is saved.
            dbContext.ChangeTracker.Clear();
            dbContext.SyncLogs.Attach(syncLog);

            syncLog.Status = SyncStatus.Failed;
            syncLog.InsertedCount = 0;
            syncLog.UpdatedCount = 0;
            syncLog.ErrorMessage = Truncate(ex.Message, MaxErrorMessageLength);
            syncLog.FinishedAtUtc = DateTime.UtcNow;

            await dbContext.SaveChangesAsync(CancellationToken.None);
        }

        return syncLog;
    }

    private async Task<(DateOnly StartDate, DateOnly EndDate)> GetDateRangeAsync(CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var latestStoredDate = await dbContext.ExchangeRates
            .Where(r => r.BaseCurrency == _options.BaseCurrency)
            .MaxAsync(r => (DateOnly?)r.Date, cancellationToken);

        // Re-fetch the latest stored day as well, in case the ECB corrected it.
        var startDate = latestStoredDate ?? today.AddDays(-_options.InitialBackfillDays);

        return (startDate, today);
    }

    private async Task<FrankfurterTimeSeriesResponse> FetchWithRetryAsync(
        DateOnly startDate,
        DateOnly endDate,
        SyncLog syncLog,
        CancellationToken cancellationToken)
    {
        for (var attempt = 1; ; attempt++)
        {
            syncLog.AttemptCount = attempt;

            try
            {
                return await frankfurterClient.GetTimeSeriesAsync(
                    _options.BaseCurrency, startDate, endDate, cancellationToken);
            }
            catch (Exception ex) when (attempt < _options.MaxRetryAttempts && IsTransient(ex, cancellationToken))
            {
                logger.LogWarning(ex,
                    "Attempt {Attempt} of {MaxAttempts} failed. Retrying in {Delay} seconds.",
                    attempt, _options.MaxRetryAttempts, _options.RetryDelaySeconds);

                await Task.Delay(TimeSpan.FromSeconds(_options.RetryDelaySeconds), cancellationToken);
            }
        }
    }

    private async Task UpsertRatesAsync(
        FrankfurterTimeSeriesResponse response,
        SyncLog syncLog,
        CancellationToken cancellationToken)
    {
        var ratesByDate = response.Rates
            .Select(entry => (
                Date: DateOnly.ParseExact(entry.Key, "yyyy-MM-dd", CultureInfo.InvariantCulture),
                Rates: entry.Value))
            .ToList();

        if (ratesByDate.Count == 0)
        {
            return;
        }

        // The API may return dates outside the requested range (e.g. the previous business day),
        // so the existing-rates lookup uses the dates actually returned.
        var minDate = ratesByDate.Min(r => r.Date);
        var maxDate = ratesByDate.Max(r => r.Date);

        var existingRates = await dbContext.ExchangeRates
            .Where(r => r.BaseCurrency == _options.BaseCurrency && r.Date >= minDate && r.Date <= maxDate)
            .ToDictionaryAsync(r => (r.TargetCurrency, r.Date), cancellationToken);

        var now = DateTime.UtcNow;

        foreach (var (date, rates) in ratesByDate)
        {
            foreach (var (targetCurrency, apiRate) in rates)
            {
                var rate = Math.Round(apiRate, RatePrecision);

                if (existingRates.TryGetValue((targetCurrency, date), out var existing))
                {
                    if (existing.Rate != rate)
                    {
                        existing.Rate = rate;
                        existing.UpdatedAtUtc = now;
                        syncLog.UpdatedCount++;
                    }

                    continue;
                }

                dbContext.ExchangeRates.Add(new ExchangeRate
                {
                    BaseCurrency = _options.BaseCurrency,
                    TargetCurrency = targetCurrency,
                    Date = date,
                    Rate = rate,
                    CreatedAtUtc = now
                });

                syncLog.InsertedCount++;
            }
        }
    }

    private static bool IsTransient(Exception ex, CancellationToken cancellationToken) =>
        ex is HttpRequestException
        || (ex is TaskCanceledException && !cancellationToken.IsCancellationRequested);

    private static string Truncate(string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..maxLength];
}

public class SyncOptions
{
    public string BaseCurrency { get; set; } = "EUR";

    public TimeOnly DailyRunTimeUtc { get; set; } = new(0, 30);

    public int InitialBackfillDays { get; set; } = 90;

    public int MaxRetryAttempts { get; set; } = 3;

    public int RetryDelaySeconds { get; set; } = 30;
}