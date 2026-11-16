using System.ComponentModel.DataAnnotations;
using ExchangeRateSync.Data;
using ExchangeRateSync.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ExchangeRateSync.Controllers;

[ApiController]
[Route("api/rates")]
public class RatesController(AppDbContext dbContext, IOptions<SyncOptions> options) : ControllerBase
{
    private readonly string _baseCurrency = options.Value.BaseCurrency;

    /// <summary>Returns all rates for the most recent available date.</summary>
    [HttpGet("latest")]
    [ProducesResponseType<LatestRatesResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<LatestRatesResponse>> GetLatest(CancellationToken cancellationToken)
    {
        var latestDate = await dbContext.ExchangeRates
            .Where(r => r.BaseCurrency == _baseCurrency)
            .MaxAsync(r => (DateOnly?)r.Date, cancellationToken);

        if (latestDate is null)
        {
            return new LatestRatesResponse(_baseCurrency, null, []);
        }

        var rates = await dbContext.ExchangeRates
            .AsNoTracking()
            .Where(r => r.BaseCurrency == _baseCurrency && r.Date == latestDate)
            .OrderBy(r => r.TargetCurrency)
            .Select(r => new RateItem(r.TargetCurrency, r.Rate))
            .ToListAsync(cancellationToken);

        return new LatestRatesResponse(_baseCurrency, latestDate, rates);
    }

    /// <summary>Returns the daily rate history of a currency for the last N days.</summary>
    [HttpGet("{currency}/history")]
    [ProducesResponseType<RateHistoryResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RateHistoryResponse>> GetHistory(
        [StringLength(3, MinimumLength = 3)] string currency,
        [FromQuery, Range(1, 365)] int days = 30,
        CancellationToken cancellationToken = default)
    {
        var targetCurrency = currency.ToUpperInvariant();
        var fromDate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-days);

        var points = await dbContext.ExchangeRates
            .AsNoTracking()
            .Where(r => r.BaseCurrency == _baseCurrency
                        && r.TargetCurrency == targetCurrency
                        && r.Date >= fromDate)
            .OrderBy(r => r.Date)
            .Select(r => new HistoryPoint(r.Date, r.Rate))
            .ToListAsync(cancellationToken);

        if (points.Count == 0)
        {
            return Problem(
                statusCode: StatusCodes.Status404NotFound,
                detail: $"No rates found for '{targetCurrency}' in the last {days} days.");
        }

        return new RateHistoryResponse(_baseCurrency, targetCurrency, points);
    }
}

public record RateItem(string Currency, decimal Rate);

public record LatestRatesResponse(string BaseCurrency, DateOnly? Date, List<RateItem> Rates);

public record HistoryPoint(DateOnly Date, decimal Rate);

public record RateHistoryResponse(string BaseCurrency, string Currency, List<HistoryPoint> Points);