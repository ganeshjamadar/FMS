using FundManager.BuildingBlocks.Audit;
using FundManager.BuildingBlocks.Domain;
using FundManager.BuildingBlocks.Financial;
using FundManager.Contracts.Events;
using FundManager.Loans.Domain.Entities;
using FundManager.Loans.Infrastructure.Data;
using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace FundManager.Loans.Domain.Services;

/// <summary>
/// Records repayment payments with idempotency and optimistic concurrency.
/// Applies payment using interest-first-then-principal order (FR-068).
/// Auto-closes the loan when principal reaches zero.
/// </summary>
public class RepaymentRecordingService
{
    private readonly LoansDbContext _db;
    private readonly IPublishEndpoint _publishEndpoint;
    private readonly AuditEventPublisher _audit;

    public RepaymentRecordingService(
        LoansDbContext db,
        IPublishEndpoint publishEndpoint,
        AuditEventPublisher audit)
    {
        _db = db;
        _publishEndpoint = publishEndpoint;
        _audit = audit;
    }

    /// <summary>
    /// Record a repayment payment against a specific repayment entry.
    /// Applies interest first, then principal, then excess to principal reduction (FR-068).
    /// Uses optimistic concurrency via xmin token.
    /// </summary>
    public async Task<Result<RepaymentResultData>> RecordRepaymentAsync(
        Guid fundId,
        Guid loanId,
        Guid repaymentId,
        decimal amount,
        Guid recordedBy,
        string? description,
        CancellationToken ct = default)
    {
        if (amount <= 0)
            return Result<RepaymentResultData>.Failure("Payment amount must be positive.", "VALIDATION");

        // Get loan with tracking
        var loan = await _db.Loans
            .FirstOrDefaultAsync(l => l.Id == loanId && l.FundId == fundId, ct);

        if (loan is null)
            return Result<RepaymentResultData>.Failure("Loan not found.", "NOT_FOUND");

        if (loan.Status != LoanStatus.Active)
            return Result<RepaymentResultData>.Failure(
                $"Cannot record repayment for loan in {loan.Status} status.",
                "INVALID_STATUS");

        // Get repayment entry with tracking
        var entry = await _db.RepaymentEntries
            .FirstOrDefaultAsync(r => r.Id == repaymentId && r.LoanId == loanId, ct);

        if (entry is null)
            return Result<RepaymentResultData>.Failure("Repayment entry not found.", "NOT_FOUND");

        if (entry.Status == RepaymentStatus.Paid)
            return Result<RepaymentResultData>.Failure("Repayment already fully paid.", "ALREADY_PAID");

        var beforeState = new
        {
            entry.AmountPaid,
            entry.Status,
            loan.OutstandingPrincipal
        };

        // Apply payment using MoneyMath: interest first, then principal, then excess (FR-068)
        var remainingInterest = entry.InterestDue - Math.Min(entry.AmountPaid, entry.InterestDue);
        var remainingPrincipal = entry.PrincipalDue - Math.Max(0, entry.AmountPaid - entry.InterestDue);

        var (interestPaid, principalPaid, excessApplied, remainingBalance) =
            MoneyMath.ApplyPayment(amount, remainingInterest, remainingPrincipal, loan.OutstandingPrincipal);

        // Update repayment entry
        entry.RecordPayment(amount);

        // Reduce loan principal
        var totalPrincipalReduction = principalPaid + excessApplied;
        if (totalPrincipalReduction > 0)
        {
            loan.ReducePrincipal(totalPrincipalReduction);
        }

        // Save with optimistic concurrency (xmin)
        await _db.SaveChangesAsync(ct);

        // Publish RepaymentRecorded event
        await _publishEndpoint.Publish(new RepaymentRecorded(
            Id: Guid.NewGuid(),
            FundId: fundId,
            LoanId: loanId,
            RepaymentEntryId: repaymentId,
            AmountPaid: amount,
            InterestPaid: interestPaid,
            PrincipalPaid: principalPaid,
            ExcessApplied: excessApplied,
            RemainingBalance: remainingBalance,
            OccurredAt: DateTime.UtcNow), ct);

        // If loan was auto-closed by ReducePrincipal, publish LoanClosed
        if (loan.Status == LoanStatus.Closed)
        {
            await _publishEndpoint.Publish(new LoanClosed(
                Id: Guid.NewGuid(),
                FundId: fundId,
                LoanId: loanId,
                OccurredAt: DateTime.UtcNow), ct);
        }

        await _audit.PublishAsync(
            fundId: fundId,
            actorId: recordedBy,
            entityType: "RepaymentEntry",
            entityId: repaymentId,
            actionType: "RepaymentRecorded",
            beforeState: beforeState,
            afterState: new
            {
                entry.AmountPaid,
                entry.Status,
                loan.OutstandingPrincipal,
                InterestPaid = interestPaid,
                PrincipalPaid = principalPaid,
                ExcessApplied = excessApplied
            },
            serviceName: "FundManager.Loans",
            cancellationToken: ct);

        return Result<RepaymentResultData>.Success(new RepaymentResultData
        {
            RepaymentId = repaymentId,
            InterestPaid = interestPaid,
            PrincipalPaid = principalPaid,
            ExcessAppliedToPrincipal = excessApplied,
            NewOutstandingPrincipal = loan.OutstandingPrincipal,
            RepaymentStatus = entry.Status.ToString(),
            LoanStatus = loan.Status.ToString()
        });
    }
}

/// <summary>
/// Result data returned after recording a repayment.
/// </summary>
public class RepaymentResultData
{
    public Guid RepaymentId { get; set; }
    public decimal InterestPaid { get; set; }
    public decimal PrincipalPaid { get; set; }
    public decimal ExcessAppliedToPrincipal { get; set; }
    public decimal NewOutstandingPrincipal { get; set; }
    public string RepaymentStatus { get; set; } = string.Empty;
    public string LoanStatus { get; set; } = string.Empty;
}
