namespace ExchangeRateSync.Entities
{
    public class SyncLog
    {
        public int Id { get; set; }

        public DateTime StartedAtUtc { get; set; }

        public DateTime? FinishedAtUtc { get; set; }

        public SyncStatus Status { get; set; }

        public SyncTrigger Trigger { get; set; }

        public int InsertedCount { get; set; }

        public int UpdatedCount { get; set; }

        public int AttemptCount { get; set; }

        public string? ErrorMessage { get; set; }
    }

    public enum SyncStatus
    {
        Running,
        Succeeded,
        Failed
    }

    public enum SyncTrigger
    {
        Scheduled,
        Manual
    }
}
