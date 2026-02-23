using FundManager.Dissolution.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace FundManager.Dissolution.Infrastructure.Data;

public class DissolutionDbContext : DbContext
{
    public DissolutionDbContext(DbContextOptions<DissolutionDbContext> options) : base(options) { }

    public DbSet<DissolutionSettlement> DissolutionSettlements => Set<DissolutionSettlement>();
    public DbSet<DissolutionLineItem> DissolutionLineItems => Set<DissolutionLineItem>();
    public DbSet<MemberProjection> MemberProjections => Set<MemberProjection>();
    public DbSet<LoanProjection> LoanProjections => Set<LoanProjection>();
    public DbSet<ContributionProjection> ContributionProjections => Set<ContributionProjection>();
    public DbSet<InterestIncomeProjection> InterestIncomeProjections => Set<InterestIncomeProjection>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("dissolution");

        // DissolutionSettlement
        modelBuilder.Entity<DissolutionSettlement>(e =>
        {
            e.ToTable("dissolution_settlements");
            e.HasKey(s => s.Id);
            e.HasIndex(s => s.FundId).IsUnique();
            e.Property(s => s.TotalInterestPool).HasColumnType("numeric(18,2)");
            e.Property(s => s.TotalContributionsCollected).HasColumnType("numeric(18,2)");
            e.Property(s => s.Status).HasConversion<string>().HasMaxLength(20);
            e.Property<uint>("xmin").IsRowVersion();
            e.HasMany(s => s.LineItems)
                .WithOne()
                .HasForeignKey(li => li.SettlementId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // DissolutionLineItem
        modelBuilder.Entity<DissolutionLineItem>(e =>
        {
            e.ToTable("dissolution_line_items");
            e.HasKey(li => li.Id);
            e.HasIndex(li => new { li.SettlementId, li.UserId }).IsUnique();
            e.Property(li => li.TotalPaidContributions).HasColumnType("numeric(18,2)");
            e.Property(li => li.InterestShare).HasColumnType("numeric(18,2)");
            e.Property(li => li.OutstandingLoanPrincipal).HasColumnType("numeric(18,2)");
            e.Property(li => li.UnpaidInterest).HasColumnType("numeric(18,2)");
            e.Property(li => li.UnpaidDues).HasColumnType("numeric(18,2)");
            e.Property(li => li.GrossPayout).HasColumnType("numeric(18,2)");
            e.Property(li => li.NetPayout).HasColumnType("numeric(18,2)");
        });

        // Projections (for cross-service data)
        modelBuilder.Entity<MemberProjection>(e =>
        {
            e.ToTable("member_projections");
            e.HasKey(m => m.Id);
            e.HasIndex(m => new { m.FundId, m.UserId }).IsUnique();
            e.Property(m => m.MonthlyContributionAmount).HasColumnType("numeric(18,2)");
        });

        modelBuilder.Entity<LoanProjection>(e =>
        {
            e.ToTable("loan_projections");
            e.HasKey(l => l.Id);
            e.Property(l => l.OutstandingPrincipal).HasColumnType("numeric(18,2)");
            e.Property(l => l.UnpaidInterest).HasColumnType("numeric(18,2)");
            e.Property(l => l.Status).HasMaxLength(30);
        });

        modelBuilder.Entity<ContributionProjection>(e =>
        {
            e.ToTable("contribution_projections");
            e.HasKey(c => c.Id);
            e.HasIndex(c => new { c.FundId, c.UserId }).IsUnique();
            e.Property(c => c.TotalPaid).HasColumnType("numeric(18,2)");
            e.Property(c => c.UnpaidAmount).HasColumnType("numeric(18,2)");
        });

        modelBuilder.Entity<InterestIncomeProjection>(e =>
        {
            e.ToTable("interest_income_projections");
            e.HasKey(i => i.Id);
            e.Property(i => i.Amount).HasColumnType("numeric(18,2)");
        });

        // Enum to string conversion for all enums
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

        // timestamptz for all DateTime properties
        foreach (var property in modelBuilder.Model.GetEntityTypes()
            .SelectMany(t => t.GetProperties())
            .Where(p => p.ClrType == typeof(DateTime) || p.ClrType == typeof(DateTime?)))
        {
            property.SetColumnType("timestamptz");
        }

        base.OnModelCreating(modelBuilder);
    }
}
