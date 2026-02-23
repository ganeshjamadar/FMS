using System.Text.Json;
using FundManager.Notifications.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace FundManager.Notifications.Infrastructure.Data;

public class NotificationsDbContext : DbContext
{
    public NotificationsDbContext(DbContextOptions<NotificationsDbContext> options) : base(options) { }

    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<NotificationPreference> NotificationPreferences => Set<NotificationPreference>();
    public DbSet<DeviceToken> DeviceTokens => Set<DeviceToken>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("notifications");

        // ── Notification ──────────────────────────────────
        modelBuilder.Entity<Notification>(entity =>
        {
            entity.ToTable("notifications");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.RecipientId).IsRequired();
            entity.Property(e => e.FundId);
            entity.Property(e => e.Channel).HasMaxLength(10).IsRequired();
            entity.Property(e => e.TemplateKey).HasMaxLength(100).IsRequired();
            entity.Property(e => e.Placeholders)
                .HasColumnType("jsonb")
                .HasDefaultValueSql("'{}'::jsonb");
            entity.Property(e => e.Status).HasMaxLength(20).IsRequired().HasDefaultValue("Pending");
            entity.Property(e => e.RetryCount).HasDefaultValue(0);
            entity.Property(e => e.MaxRetries).HasDefaultValue(3);
            entity.Property(e => e.NextRetryAt);
            entity.Property(e => e.ScheduledAt).HasDefaultValueSql("now()");
            entity.Property(e => e.SentAt);
            entity.Property(e => e.FailedAt);
            entity.Property(e => e.FailureReason);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");

            // Indexes for feed queries and retry processing
            entity.HasIndex(e => new { e.RecipientId, e.Channel, e.Status });
            entity.HasIndex(e => new { e.Status, e.NextRetryAt });
            entity.HasIndex(e => e.FundId);
        });

        // ── NotificationPreference ────────────────────────
        modelBuilder.Entity<NotificationPreference>(entity =>
        {
            entity.ToTable("notification_preferences");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.UserId).IsRequired();
            entity.Property(e => e.Channel).HasMaxLength(10).IsRequired();
            entity.Property(e => e.Enabled).HasDefaultValue(true);
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("now()");

            // Unique: (UserId, Channel)
            entity.HasIndex(e => new { e.UserId, e.Channel }).IsUnique();
        });

        // ── DeviceToken ───────────────────────────────────
        modelBuilder.Entity<DeviceToken>(entity =>
        {
            entity.ToTable("device_tokens");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.UserId).IsRequired();
            entity.Property(e => e.DeviceId).HasMaxLength(255).IsRequired();
            entity.Property(e => e.PushToken).IsRequired();
            entity.Property(e => e.Platform).HasMaxLength(10).IsRequired();
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("now()");

            // Unique: (UserId, DeviceId)
            entity.HasIndex(e => new { e.UserId, e.DeviceId }).IsUnique();
        });

        // Global conventions
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            foreach (var property in entityType.GetProperties())
            {
                if (property.ClrType.IsEnum)
                {
                    property.SetMaxLength(30);
                }
            }
        }

        foreach (var property in modelBuilder.Model.GetEntityTypes()
            .SelectMany(t => t.GetProperties())
            .Where(p => p.ClrType == typeof(DateTime) || p.ClrType == typeof(DateTime?)))
        {
            property.SetColumnType("timestamptz");
        }

        base.OnModelCreating(modelBuilder);
    }
}
