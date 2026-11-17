using ExchangeRateSync.Entities;
using Microsoft.EntityFrameworkCore;

namespace ExchangeRateSync.Data
{
    public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
    {
        public DbSet<ExchangeRate> ExchangeRates => Set<ExchangeRate>();

        public DbSet<SyncLog> SyncLogs => Set<SyncLog>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<ExchangeRate>(entity =>
            {
                entity.Property(e => e.BaseCurrency)
                    .HasMaxLength(3)
                    .IsRequired();

                entity.Property(e => e.TargetCurrency)
                    .HasMaxLength(3)
                    .IsRequired();

                entity.Property(e => e.Rate)
                    .HasPrecision(18, 6);

                // One rate per currency pair per day — the basis of the idempotent upsert.
                entity.HasIndex(e => new { e.BaseCurrency, e.TargetCurrency, e.Date })
                    .IsUnique();

                entity.HasIndex(e => e.Date);
            });

            modelBuilder.Entity<SyncLog>(entity =>
            {
                entity.Property(e => e.Status)
                    .HasConversion<string>()
                    .HasMaxLength(20);

                entity.Property(e => e.Trigger)
                    .HasConversion<string>()
                    .HasMaxLength(20);

                entity.Property(e => e.ErrorMessage)
                    .HasMaxLength(2000);

                // SQL Server's datetime2 does not store DateTimeKind; mark values read from the database as UTC
                // so they are serialized with a trailing "Z".
                entity.Property(e => e.StartedAtUtc)
                    .HasConversion(v => v, v => DateTime.SpecifyKind(v, DateTimeKind.Utc));

                entity.Property(e => e.FinishedAtUtc)
                    .HasConversion(v => v, v => v.HasValue ? DateTime.SpecifyKind(v.Value, DateTimeKind.Utc) : v);

                entity.HasIndex(e => e.StartedAtUtc);
            });
        }
    }
}