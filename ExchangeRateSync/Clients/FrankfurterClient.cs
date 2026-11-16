using System.Globalization;

namespace ExchangeRateSync.Clients
{
    public class FrankfurterClient(HttpClient httpClient)
    {
        public async Task<FrankfurterTimeSeriesResponse> GetTimeSeriesAsync(
            string baseCurrency,
            DateOnly startDate,
            DateOnly endDate,
            CancellationToken cancellationToken = default)
        {
            var start = startDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            var end = endDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            var url = $"{start}..{end}?base={Uri.EscapeDataString(baseCurrency)}";

            var response = await httpClient.GetFromJsonAsync<FrankfurterTimeSeriesResponse>(url, cancellationToken);

            return response ?? throw new InvalidOperationException("Frankfurter API returned an empty response.");
        }
    }

    public record FrankfurterTimeSeriesResponse(
        string Base,
        Dictionary<string, Dictionary<string, decimal>> Rates);

}