using FundManager.Identity.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace FundManager.Identity.Infrastructure.Data;

public class IdentityDbContext : DbContext
{
    public IdentityDbContext(DbContextOptions<IdentityDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<OtpChallenge> OtpChallenges => Set<OtpChallenge>();
    public DbSet<Session> Sessions => Set<Session>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("identity");

        // ── User ──
        modelBuilder.Entity<User>(e =>
        {
            e.ToTable("users");
            e.HasKey(u => u.Id);
            e.Property(u => u.Name).HasMaxLength(255).IsRequired();
            e.Property(u => u.Phone).HasMaxLength(20);
            e.Property(u => u.Email).HasMaxLength(255);
            e.Property(u => u.ProfilePictureUrl).HasColumnType("text");
            e.Property(u => u.IsActive).HasDefaultValue(true);
            e.Property<uint>("xmin").IsRowVersion();

            // Unique partial indexes for phone/email lookup
            e.HasIndex(u => u.Phone).IsUnique().HasFilter("\"Phone\" IS NOT NULL").HasDatabaseName("ix_users_phone");
            e.HasIndex(u => u.Email).IsUnique().HasFilter("\"Email\" IS NOT NULL").HasDatabaseName("ix_users_email");
        });

        // ── OtpChallenge ──
        modelBuilder.Entity<OtpChallenge>(e =>
        {
            e.ToTable("otp_challenges");
            e.HasKey(o => o.Id);
            e.Property(o => o.Channel).HasMaxLength(10).IsRequired();
            e.Property(o => o.Target).HasMaxLength(255).IsRequired();
            e.Property(o => o.OtpHash).HasMaxLength(128).IsRequired();
            e.Property(o => o.Verified).HasDefaultValue(false);
            e.Property(o => o.Attempts).HasDefaultValue(0);

            e.HasIndex(o => new { o.Target, o.CreatedAt }).HasDatabaseName("ix_otp_target_created");
        });

        // ── Session ──
        modelBuilder.Entity<Session>(e =>
        {
            e.ToTable("sessions");
            e.HasKey(s => s.Id);
            e.Property(s => s.TokenHash).HasMaxLength(128).IsRequired();
            e.Property(s => s.IpAddress).HasColumnType("text");
            e.Property(s => s.UserAgent).HasColumnType("text");
            e.Property(s => s.Revoked).HasDefaultValue(false);

            e.HasIndex(s => new { s.UserId, s.Revoked }).HasDatabaseName("ix_sessions_user_active");
            e.HasIndex(s => s.TokenHash).IsUnique().HasDatabaseName("ix_sessions_token");
        });

        // Global: store all enums as strings
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

        // Global: DateTime → timestamptz
        foreach (var property in modelBuilder.Model.GetEntityTypes()
            .SelectMany(t => t.GetProperties())
            .Where(p => p.ClrType == typeof(DateTime) || p.ClrType == typeof(DateTime?)))
        {
            property.SetColumnType("timestamptz");
        }

        base.OnModelCreating(modelBuilder);
    }
}
