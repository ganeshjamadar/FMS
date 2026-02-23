using FundManager.Contracts.Events;
using FundManager.Loans.Domain.Entities;
using FundManager.Loans.Infrastructure.Data;
using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace FundManager.Loans.Infrastructure.Consumers;

/// <summary>
/// Maintains local FundProjection when a fund is created.
/// FundCreated carries the config fields we need for loan validation.
/// </summary>
public class FundCreatedConsumer : IConsumer<FundCreated>
{
    private readonly LoansDbContext _db;

    public FundCreatedConsumer(LoansDbContext db)
    {
        _db = db;
    }

    public async Task Consume(ConsumeContext<FundCreated> context)
    {
        var msg = context.Message;

        // Idempotency check
        var existing = await _db.FundProjections
            .FirstOrDefaultAsync(f => f.FundId == msg.FundId);

        if (existing is not null) return;

        var projection = FundProjection.Create(
            fundId: msg.FundId,
            monthlyInterestRate: msg.MonthlyInterestRate,
            minimumPrincipalPerRepayment: msg.MinimumPrincipalPerRepayment,
            maxLoanPerMember: msg.MaxLoanPerMember,
            maxConcurrentLoans: msg.MaxConcurrentLoans,
            loanApprovalPolicy: msg.LoanApprovalPolicy);

        _db.FundProjections.Add(projection);
        await _db.SaveChangesAsync();
    }
}
