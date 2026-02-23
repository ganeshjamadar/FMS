using FundManager.Contracts.Events;
using FundManager.Contributions.Domain.Entities;
using FundManager.Contributions.Infrastructure.Data;
using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace FundManager.Contributions.Infrastructure.Consumers;

/// <summary>
/// Creates a disbursement Transaction in the fund ledger when a loan is approved and disbursed.
/// The disbursement reduces the fund pool balance.
/// </summary>
public class LoanDisbursedConsumer : IConsumer<LoanDisbursed>
{
    private readonly ContributionsDbContext _db;

    public LoanDisbursedConsumer(ContributionsDbContext db)
    {
        _db = db;
    }

    public async Task Consume(ConsumeContext<LoanDisbursed> context)
    {
        var msg = context.Message;
        var idempotencyKey = $"disbursement-{msg.LoanId}";

        // Idempotency check: if transaction already exists, skip
        var existing = await _db.Transactions
            .AnyAsync(t => t.FundId == msg.FundId && t.IdempotencyKey == idempotencyKey);

        if (existing) return;

        var transaction = Transaction.Create(
            fundId: msg.FundId,
            userId: msg.BorrowerId,
            type: TransactionType.Disbursement,
            amount: msg.PrincipalAmount,
            idempotencyKey: idempotencyKey,
            recordedBy: msg.BorrowerId, // System action on behalf of borrower
            referenceEntityType: "Loan",
            referenceEntityId: msg.LoanId,
            description: $"Loan disbursement of {msg.PrincipalAmount:N2}");

        _db.Transactions.Add(transaction);
        await _db.SaveChangesAsync();
    }
}
