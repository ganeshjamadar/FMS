using FundManager.Loans.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace FundManager.Loans.Infrastructure.Data;

public class LoansDbContext : DbContext
{
    public LoansDbContext(DbContextOptions<LoansDbContext> options) : base(options) { }

    public DbSet<Loan> Loans => Set<Loan>();
    public DbSet<RepaymentEntry> RepaymentEntries => Set<RepaymentEntry>();
    public DbSet<VotingSession> VotingSessions => Set<VotingSession>();
    public DbSet<Vote> Votes => Set<Vote>();
    public DbSet<FundProjection> FundProjections => Set<FundProjection>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("loans");

        // ── Loan ──
        modelBuilder.Entity<Loan>(e =>
        {
            e.ToTable("loans");
            e.HasKey(l => l.Id);

            e.Property(l => l.PrincipalAmount).HasColumnType("numeric(18,2)");
            e.Property(l => l.OutstandingPrincipal).HasColumnType("numeric(18,2)");
            e.Property(l => l.MonthlyInterestRate).HasColumnType("numeric(8,6)");
            e.Property(l => l.ScheduledInstallment).HasColumnType("numeric(18,2)");
            e.Property(l => l.MinimumPrincipal).HasColumnType("numeric(18,2)");
            e.Property(l => l.Status).HasMaxLength(30)
                .HasConversion(v => v.ToString(), v => Enum.Parse<LoanStatus>(v));
            e.Property(l => l.Purpose).HasColumnType("text");
            e.Property(l => l.RejectionReason).HasColumnType("text");

            // xmin optimistic concurrency
            e.Property<uint>("xmin")
                .HasColumnType("xid")
                .ValueGeneratedOnAddOrUpdate()
                .IsConcurrencyToken();

            e.HasIndex(l => new { l.FundId, l.BorrowerId });
            e.HasIndex(l => new { l.FundId, l.Status });
        });

        // ── RepaymentEntry ──
        modelBuilder.Entity<RepaymentEntry>(e =>
        {
            e.ToTable("repayment_entries");
            e.HasKey(r => r.Id);

            e.Property(r => r.InterestDue).HasColumnType("numeric(18,2)");
            e.Property(r => r.PrincipalDue).HasColumnType("numeric(18,2)");
            e.Property(r => r.TotalDue).HasColumnType("numeric(18,2)");
            e.Property(r => r.AmountPaid).HasColumnType("numeric(18,2)");
            e.Property(r => r.Status).HasMaxLength(20)
                .HasConversion(v => v.ToString(), v => Enum.Parse<RepaymentStatus>(v));

            // xmin optimistic concurrency
            e.Property<uint>("xmin")
                .HasColumnType("xid")
                .ValueGeneratedOnAddOrUpdate()
                .IsConcurrencyToken();

            // Unique: one entry per loan per month
            e.HasIndex(r => new { r.LoanId, r.MonthYear }).IsUnique();
            e.HasIndex(r => new { r.FundId, r.LoanId });
            e.HasIndex(r => r.Status);
        });

        // ── VotingSession ──
        modelBuilder.Entity<VotingSession>(e =>
        {
            e.ToTable("voting_sessions");
            e.HasKey(v => v.Id);

            e.Property(v => v.ThresholdType).HasMaxLength(20);
            e.Property(v => v.ThresholdValue).HasColumnType("numeric(5,2)");
            e.Property(v => v.Result).HasMaxLength(20)
                .HasConversion(v => v.ToString(), v => Enum.Parse<VotingResult>(v));

            // xmin optimistic concurrency
            e.Property<uint>("xmin")
                .HasColumnType("xid")
                .ValueGeneratedOnAddOrUpdate()
                .IsConcurrencyToken();

            // Unique: one session per loan
            e.HasIndex(v => v.LoanId).IsUnique();
            e.HasIndex(v => v.FundId);

            e.HasMany(v => v.Votes)
                .WithOne()
                .HasForeignKey(vote => vote.VotingSessionId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // ── Vote ──
        modelBuilder.Entity<Vote>(e =>
        {
            e.ToTable("votes");
            e.HasKey(v => v.Id);

            e.Property(v => v.Decision).HasMaxLength(10);

            // Unique: one vote per voter per session
            e.HasIndex(v => new { v.VotingSessionId, v.VoterId }).IsUnique();
        });

        // ── FundProjection ──
        modelBuilder.Entity<FundProjection>(e =>
        {
            e.ToTable("fund_projections");
            e.HasKey(f => f.Id);

            e.Property(f => f.MonthlyInterestRate).HasColumnType("numeric(8,6)");
            e.Property(f => f.MinimumPrincipalPerRepayment).HasColumnType("numeric(18,2)");
            e.Property(f => f.MaxLoanPerMember).HasColumnType("numeric(18,2)");
            e.Property(f => f.LoanApprovalPolicy).HasMaxLength(30);

            e.HasIndex(f => f.FundId).IsUnique();
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
