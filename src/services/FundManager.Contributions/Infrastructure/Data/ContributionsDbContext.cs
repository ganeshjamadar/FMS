using FundManager.Contributions.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace FundManager.Contributions.Infrastructure.Data;

public class ContributionsDbContext : DbContext
{
    public ContributionsDbContext(DbContextOptions<ContributionsDbContext> options) : base(options) { }

    public DbSet<ContributionDue> ContributionDues => Set<ContributionDue>();
    public DbSet<Transaction> Transactions => Set<Transaction>();
    public DbSet<IdempotencyRecord> IdempotencyRecords => Set<IdempotencyRecord>();
    public DbSet<MemberProjection> MemberProjections => Set<MemberProjection>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("contributions");

        // ── ContributionDue ──
        modelBuilder.Entity<ContributionDue>(e =>
        {
            e.ToTable("contribution_dues");
            e.HasKey(d => d.Id);
            e.Property(d => d.AmountDue).HasColumnType("numeric(18,2)");
            e.Property(d => d.AmountPaid).HasColumnType("numeric(18,2)");
            e.Property(d => d.RemainingBalance).HasColumnType("numeric(18,2)");
            e.Property(d => d.Status).HasConversion<string>().HasMaxLength(20);
            e.HasIndex(d => new { d.UserId, d.FundId, d.MonthYear }).IsUnique()
                .HasDatabaseName("ix_contribution_dues_user_fund_month");
            e.HasIndex(d => new { d.FundId, d.MonthYear })
                .HasDatabaseName("ix_contribution_dues_fund_month");
            e.HasIndex(d => new { d.FundId, d.Status })
                .HasDatabaseName("ix_contribution_dues_fund_status");
            // xmin optimistic concurrency
            e.Property<uint>("xmin")
                .HasColumnName("xmin")
                .HasColumnType("xid")
                .ValueGeneratedOnAddOrUpdate()
                .IsConcurrencyToken();
            e.Property(d => d.RowVersion).HasColumnName("xmin_shadow").ValueGeneratedOnAddOrUpdate();
        });

        // ── Transaction (append-only) ──
        modelBuilder.Entity<Transaction>(e =>
        {
            e.ToTable("transactions");
            e.HasKey(t => t.Id);
            e.Property(t => t.Amount).HasColumnType("numeric(18,2)");
            e.Property(t => t.Type).HasConversion<string>().HasMaxLength(30);
            e.Property(t => t.IdempotencyKey).HasMaxLength(64);
            e.Property(t => t.ReferenceEntityType).HasMaxLength(30);
            e.HasIndex(t => new { t.FundId, t.IdempotencyKey }).IsUnique()
                .HasDatabaseName("ix_transactions_fund_idempotency");
            e.HasIndex(t => new { t.FundId, t.CreatedAt })
                .HasDatabaseName("ix_transactions_fund_created");
            e.HasIndex(t => new { t.FundId, t.Type })
                .HasDatabaseName("ix_transactions_fund_type");
        });

        // ── IdempotencyRecord ──
        modelBuilder.Entity<IdempotencyRecord>(e =>
        {
            e.ToTable("idempotency_records");
            e.HasKey(r => r.Id);
            e.Property(r => r.IdempotencyKey).HasMaxLength(64);
            e.Property(r => r.Endpoint).HasMaxLength(100);
            e.Property(r => r.RequestHash).HasMaxLength(64);
            e.HasIndex(r => new { r.FundId, r.IdempotencyKey, r.Endpoint }).IsUnique()
                .HasDatabaseName("ix_idempotency_fund_key_endpoint");
        });

        // ── MemberProjection ──
        modelBuilder.Entity<MemberProjection>(e =>
        {
            e.ToTable("member_projections");
            e.HasKey(m => m.Id);
            e.Property(m => m.MonthlyContributionAmount).HasColumnType("numeric(18,2)");
            e.HasIndex(m => new { m.FundId, m.IsActive })
                .HasDatabaseName("ix_member_projections_fund_active");
            e.HasIndex(m => new { m.UserId, m.FundId }).IsUnique()
                .HasDatabaseName("ix_member_projections_user_fund");
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
