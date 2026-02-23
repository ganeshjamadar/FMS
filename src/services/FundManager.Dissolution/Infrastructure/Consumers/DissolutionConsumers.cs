using FundManager.Contracts.Events;
using FundManager.Dissolution.Domain.Entities;
using FundManager.Dissolution.Infrastructure.Data;
using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace FundManager.Dissolution.Infrastructure.Consumers;

/// <summary>
/// Maintains MemberProjection when a member joins a fund.
/// </summary>
public class MemberJoinedConsumer : IConsumer<MemberJoined>
{
    private readonly DissolutionDbContext _db;

    public MemberJoinedConsumer(DissolutionDbContext db) => _db = db;

    public async Task Consume(ConsumeContext<MemberJoined> context)
    {
        var evt = context.Message;
        var exists = await _db.MemberProjections
            .AnyAsync(m => m.FundId == evt.FundId && m.UserId == evt.UserId, context.CancellationToken);

        if (!exists)
        {
            _db.MemberProjections.Add(new MemberProjection
            {
                Id = Guid.NewGuid(),
                FundId = evt.FundId,
                UserId = evt.UserId,
                MonthlyContributionAmount = evt.MonthlyContributionAmount,
                CreatedAt = DateTime.UtcNow,
            });
            await _db.SaveChangesAsync(context.CancellationToken);
        }
    }
}

/// <summary>
/// Maintains LoanProjection when a loan is disbursed.
/// </summary>
public class LoanDisbursedConsumer : IConsumer<LoanDisbursed>
{
    private readonly DissolutionDbContext _db;

    public LoanDisbursedConsumer(DissolutionDbContext db) => _db = db;

    public async Task Consume(ConsumeContext<LoanDisbursed> context)
    {
        var evt = context.Message;
        var existing = await _db.LoanProjections
            .FirstOrDefaultAsync(l => l.Id == evt.LoanId, context.CancellationToken);

        if (existing is null)
        {
            _db.LoanProjections.Add(new LoanProjection
            {
                Id = evt.LoanId,
                FundId = evt.FundId,
                BorrowerId = evt.BorrowerId,
                OutstandingPrincipal = evt.PrincipalAmount,
                UnpaidInterest = 0m,
                Status = "Active",
                UpdatedAt = DateTime.UtcNow,
            });
            await _db.SaveChangesAsync(context.CancellationToken);
        }
    }
}

/// <summary>
/// Updates LoanProjection when a repayment is recorded.
/// </summary>
public class RepaymentRecordedConsumer : IConsumer<RepaymentRecorded>
{
    private readonly DissolutionDbContext _db;

    public RepaymentRecordedConsumer(DissolutionDbContext db) => _db = db;

    public async Task Consume(ConsumeContext<RepaymentRecorded> context)
    {
        var evt = context.Message;
        var loan = await _db.LoanProjections
            .FirstOrDefaultAsync(l => l.Id == evt.LoanId, context.CancellationToken);

        if (loan is not null)
        {
            loan.OutstandingPrincipal = evt.RemainingBalance;
            loan.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(context.CancellationToken);
        }
    }
}

/// <summary>
/// Updates LoanProjection when a loan is closed.
/// </summary>
public class LoanClosedConsumer : IConsumer<LoanClosed>
{
    private readonly DissolutionDbContext _db;

    public LoanClosedConsumer(DissolutionDbContext db) => _db = db;

    public async Task Consume(ConsumeContext<LoanClosed> context)
    {
        var evt = context.Message;
        var loan = await _db.LoanProjections
            .FirstOrDefaultAsync(l => l.Id == evt.LoanId, context.CancellationToken);

        if (loan is not null)
        {
            loan.OutstandingPrincipal = 0m;
            loan.UnpaidInterest = 0m;
            loan.Status = "Closed";
            loan.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(context.CancellationToken);
        }
    }
}

/// <summary>
/// Tracks contribution payments for dissolution settlement calculations.
/// </summary>
public class ContributionPaidConsumer : IConsumer<ContributionPaid>
{
    private readonly DissolutionDbContext _db;

    public ContributionPaidConsumer(DissolutionDbContext db) => _db = db;

    public async Task Consume(ConsumeContext<ContributionPaid> context)
    {
        var evt = context.Message;
        var projection = await _db.ContributionProjections
            .FirstOrDefaultAsync(c => c.FundId == evt.FundId && c.UserId == evt.UserId, context.CancellationToken);

        if (projection is null)
        {
            projection = new ContributionProjection
            {
                Id = Guid.NewGuid(),
                FundId = evt.FundId,
                UserId = evt.UserId,
                TotalPaid = 0m,
                UnpaidAmount = 0m,
                UpdatedAt = DateTime.UtcNow,
            };
            _db.ContributionProjections.Add(projection);
        }

        projection.TotalPaid += evt.AmountPaid;
        projection.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(context.CancellationToken);
    }
}

/// <summary>
/// Tracks interest income for dissolution settlement calculations.
/// </summary>
public class InterestIncomeConsumer : IConsumer<RepaymentRecorded>
{
    private readonly DissolutionDbContext _db;

    // Note: Reuses RepaymentRecorded event — separate consumer for interest tracking
    public InterestIncomeConsumer(DissolutionDbContext db) => _db = db;

    public async Task Consume(ConsumeContext<RepaymentRecorded> context)
    {
        var evt = context.Message;
        if (evt.InterestPaid > 0)
        {
            _db.InterestIncomeProjections.Add(new InterestIncomeProjection
            {
                Id = Guid.NewGuid(),
                FundId = evt.FundId,
                Amount = evt.InterestPaid,
                RecordedAt = DateTime.UtcNow,
            });
            await _db.SaveChangesAsync(context.CancellationToken);
        }
    }
}
