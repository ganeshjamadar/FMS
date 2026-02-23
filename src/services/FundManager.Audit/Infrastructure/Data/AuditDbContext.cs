using FundManager.Audit.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace FundManager.Audit.Infrastructure.Data;

public class AuditDbContext : DbContext
{
    public AuditDbContext(DbContextOptions<AuditDbContext> options) : base(options) { }

    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("audit");

        // AuditLog table configuration
        modelBuilder.Entity<AuditLog>(entity =>
        {
            entity.ToTable("audit_logs");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnType("uuid").HasDefaultValueSql("gen_random_uuid()");
            entity.Property(e => e.ActionType).HasMaxLength(50).IsRequired();
            entity.Property(e => e.EntityType).HasMaxLength(50).IsRequired();
            entity.Property(e => e.EntityId).HasColumnType("uuid").IsRequired();
            entity.Property(e => e.BeforeState).HasColumnType("jsonb");
            entity.Property(e => e.AfterState).HasColumnType("jsonb");
            entity.Property(e => e.IpAddress).HasColumnType("text");
            entity.Property(e => e.ServiceName).HasMaxLength(50).IsRequired();

            entity.HasIndex(e => e.FundId);
            entity.HasIndex(e => e.ActorId);
            entity.HasIndex(e => e.Timestamp);
            entity.HasIndex(e => new { e.EntityType, e.EntityId });
        });

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
