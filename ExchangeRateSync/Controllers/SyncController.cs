using System.ComponentModel.DataAnnotations;
using ExchangeRateSync.Data;
using ExchangeRateSync.Entities;
using ExchangeRateSync.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ExchangeRateSync.Controllers;

[ApiController]
[Route("api/sync")]
public class SyncController(AppDbContext dbContext, ExchangeRateSyncService syncService) : ControllerBase
{
    /// <summary>Returns the most recent synchronization runs, newest first.</summary>
    [HttpGet("logs")]
    [ProducesResponseType<List<SyncLogItem>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<List<SyncLogItem>>> GetLogs(
        [FromQuery, Range(1, 100)] int take = 20,
        CancellationToken cancellationToken = default)
    {
        var logs = await dbContext.SyncLogs
            .AsNoTracking()
            .OrderByDescending(l => l.StartedAtUtc)
            .Take(take)
            .ToListAsync(cancellationToken);

        return logs.Select(ToItem).ToList();
    }

    /// <summary>Starts a synchronization immediately and returns its result.</summary>
    [HttpPost]
    [ProducesResponseType<SyncLogItem>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<SyncLogItem>> RunNow()
    {
        try
        {
            // Not tied to the request's cancellation token: closing the browser
            // should not abort a sync that has already started.
            var syncLog = await syncService.RunAsync(SyncTrigger.Manual, CancellationToken.None);
            return ToItem(syncLog);
        }
        catch (InvalidOperationException ex)
        {
            return Problem(statusCode: StatusCodes.Status409Conflict, detail: ex.Message);
        }
    }

    private static SyncLogItem ToItem(SyncLog log) => new(
        log.Id,
        log.StartedAtUtc,
        log.FinishedAtUtc,
        log.FinishedAtUtc is null ? null : (log.FinishedAtUtc.Value - log.StartedAtUtc).TotalSeconds,
        log.Status,
        log.Trigger,
        log.InsertedCount,
        log.UpdatedCount,
        log.AttemptCount,
        log.ErrorMessage);
}

public record SyncLogItem(
    int Id,
    DateTime StartedAtUtc,
    DateTime? FinishedAtUtc,
    double? DurationSeconds,
    SyncStatus Status,
    SyncTrigger Trigger,
    int InsertedCount,
    int UpdatedCount,
    int AttemptCount,
    string? ErrorMessage);