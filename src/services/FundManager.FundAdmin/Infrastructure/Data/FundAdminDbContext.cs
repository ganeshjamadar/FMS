using FundManager.FundAdmin.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace FundManager.FundAdmin.Infrastructure.Data;

public class FundAdminDbContext : DbContext
{
    public FundAdminDbContext(DbContextOptions<FundAdminDbContext> options) : base(options) { }

    public DbSet<Fund> Funds => Set<Fund>();
    public DbSet<FundRoleAssignment> FundRoleAssignments => Set<FundRoleAssignment>();
    public DbSet<MemberContributionPlan> MemberContributionPlans => Set<MemberContributionPlan>();
    public DbSet<Invitation> Invitations => Set<Invitation>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("fundadmin");

        // ── Fund ──
        modelBuilder.Entity<Fund>(e =>
        {
            e.ToTable("funds");
            e.HasKey(f => f.Id);
            e.Property(f => f.Name).HasMaxLength(255).IsRequired();
            e.Property(f => f.Description).HasColumnType("text");
            e.Property(f => f.Currency).HasMaxLength(3).HasDefaultValue("INR");
            e.Property(f => f.MonthlyInterestRate).HasColumnType("numeric(8,6)");
            e.Property(f => f.MinimumMonthlyContribution).HasColumnType("numeric(18,2)");
            e.Property(f => f.MinimumPrincipalPerRepayment).HasColumnType("numeric(18,2)").HasDefaultValue(1000.00m);
            e.Property(f => f.LoanApprovalPolicy).HasMaxLength(30).HasDefaultValue("AdminOnly");
            e.Property(f => f.MaxLoanPerMember).HasColumnType("numeric(18,2)");
            e.Property(f => f.MaxConcurrentLoans);
            e.Property(f => f.DissolutionPolicy).HasColumnType("text");
            e.Property(f => f.OverduePenaltyType).HasMaxLength(20).HasDefaultValue("None");
            e.Property(f => f.OverduePenaltyValue).HasColumnType("numeric(18,2)").HasDefaultValue(0.00m);
            e.Property(f => f.ContributionDayOfMonth).HasDefaultValue(1);
            e.Property(f => f.GracePeriodDays).HasDefaultValue(5);
            e.Property(f => f.State).HasMaxLength(20).HasConversion<string>();
            e.Property<uint>("xmin").IsRowVersion();

            e.HasMany(f => f.RoleAssignments)
                .WithOne(r => r.Fund)
                .HasForeignKey(r => r.FundId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasMany(f => f.MemberPlans)
                .WithOne(p => p.Fund)
                .HasForeignKey(p => p.FundId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // ── FundRoleAssignment ──
        modelBuilder.Entity<FundRoleAssignment>(e =>
        {
            e.ToTable("fund_role_assignments");
            e.HasKey(r => r.Id);
            e.Property(r => r.Role).HasMaxLength(20).IsRequired();

            e.HasIndex(r => new { r.UserId, r.FundId }).IsUnique().HasDatabaseName("ix_roles_user_fund");
            e.HasIndex(r => r.UserId).HasDatabaseName("ix_roles_user");
            e.HasIndex(r => new { r.FundId, r.Role }).HasDatabaseName("ix_roles_fund_role");
        });

        // ── MemberContributionPlan ──
        modelBuilder.Entity<MemberContributionPlan>(e =>
        {
            e.ToTable("member_contribution_plans");
            e.HasKey(p => p.Id);
            e.Property(p => p.MonthlyContributionAmount).HasColumnType("numeric(18,2)");
            e.Property(p => p.JoinDate).HasColumnType("date");
            e.Property(p => p.IsActive).HasDefaultValue(true);

            e.HasIndex(p => new { p.UserId, p.FundId }).IsUnique().HasDatabaseName("ix_plans_user_fund");
        });

        // ── Invitation ──
        modelBuilder.Entity<Invitation>(e =>
        {
            e.ToTable("invitations");
            e.HasKey(i => i.Id);
            e.Property(i => i.TargetContact).HasMaxLength(255).IsRequired();
            e.Property(i => i.Status).HasMaxLength(20).HasConversion<string>();
            e.Property(i => i.ExpiresAt).HasColumnType("timestamptz");
            e.Property(i => i.RespondedAt).HasColumnType("timestamptz");

            e.HasIndex(i => new { i.FundId, i.Status }).HasDatabaseName("ix_invitations_fund_status");
            e.HasIndex(i => new { i.TargetContact, i.FundId }).HasDatabaseName("ix_invitations_contact_fund");

            e.HasOne(i => i.Fund)
                .WithMany()
                .HasForeignKey(i => i.FundId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Global: store all non-configured enums as strings
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            foreach (var property in entityType.GetProperties())
            {
                if (property.ClrType.IsEnum && property.GetMaxLength() is null)
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
