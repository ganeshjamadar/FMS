using FundManager.Contracts.Events;
using FundManager.Contributions.Domain.Entities;
using FundManager.Contributions.Infrastructure.Data;
using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace FundManager.Contributions.Infrastructure.Consumers;

/// <summary>
/// Creates repayment and interest-income Transactions in the fund ledger
/// when a loan repayment is recorded. The repayment principal increases the
/// fund pool balance, and interest income is recorded separately.
/// </summary>
public class RepaymentReceivedConsumer : IConsumer<RepaymentRecorded>
{
    private readonly ContributionsDbContext _db;

    public RepaymentReceivedConsumer(ContributionsDbContext db)
    {
        _db = db;
    }

    public async Task Consume(ConsumeContext<RepaymentRecorded> context)
    {
        var msg = context.Message;

        // Repayment principal transaction (principal + excess applied)
        var principalTotal = msg.PrincipalPaid + msg.ExcessApplied;
        if (principalTotal > 0)
        {
            var repaymentKey = $"repayment-principal-{msg.RepaymentEntryId}-{msg.Id}";
            var existingRepayment = await _db.Transactions
                .AnyAsync(t => t.FundId == msg.FundId && t.IdempotencyKey == repaymentKey);

            if (!existingRepayment)
            {
                var repaymentTx = Transaction.Create(
                    fundId: msg.FundId,
                    userId: Guid.Empty, // System records on behalf of borrower
                    type: TransactionType.Repayment,
                    amount: principalTotal,
                    idempotencyKey: repaymentKey,
                    recordedBy: Guid.Empty,
                    referenceEntityType: "RepaymentEntry",
                    referenceEntityId: msg.RepaymentEntryId,
                    description: $"Loan repayment principal {principalTotal:N2} for loan {msg.LoanId}");

                _db.Transactions.Add(repaymentTx);
            }
        }

        // Interest income transaction
        if (msg.InterestPaid > 0)
        {
            var interestKey = $"repayment-interest-{msg.RepaymentEntryId}-{msg.Id}";
            var existingInterest = await _db.Transactions
                .AnyAsync(t => t.FundId == msg.FundId && t.IdempotencyKey == interestKey);

            if (!existingInterest)
            {
                var interestTx = Transaction.Create(
                    fundId: msg.FundId,
                    userId: Guid.Empty,
                    type: TransactionType.InterestIncome,
                    amount: msg.InterestPaid,
                    idempotencyKey: interestKey,
                    recordedBy: Guid.Empty,
                    referenceEntityType: "RepaymentEntry",
                    referenceEntityId: msg.RepaymentEntryId,
                    description: $"Interest income {msg.InterestPaid:N2} from loan {msg.LoanId}");

                _db.Transactions.Add(interestTx);
            }
        }

        await _db.SaveChangesAsync();
    }
}
