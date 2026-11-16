namespace ExchangeRateSync.Entities
{
    public class ExchangeRate
    {
        public int Id { get; set; }

        public string BaseCurrency { get; set; } = string.Empty;

        public string TargetCurrency { get; set; } = string.Empty;

        public decimal Rate { get; set; }

        public DateOnly Date { get; set; }

        public DateTime CreatedAtUtc { get; set; }

        public DateTime? UpdatedAtUtc { get; set; }
    }
}
