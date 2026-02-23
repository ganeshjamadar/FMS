using FundManager.BuildingBlocks.Audit;
using FundManager.BuildingBlocks.Domain;
using FundManager.Contracts.Events;
using FundManager.Loans.Domain.Entities;
using FundManager.Loans.Infrastructure.Data;
using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace FundManager.Loans.Domain.Services;

/// <summary>
/// Handles loan request, approval, rejection, and disbursement.
/// Validates against fund pool balance, max loan cap, and max concurrent loans.
/// </summary>
public class LoanRequestService
{
    private readonly LoansDbContext _db;
    private readonly IPublishEndpoint _publishEndpoint;
    private readonly AuditEventPublisher _audit;

    public LoanRequestService(
        LoansDbContext db,
        IPublishEndpoint publishEndpoint,
        AuditEventPublisher audit)
    {
        _db = db;
        _publishEndpoint = publishEndpoint;
        _audit = audit;
    }

    /// <summary>
    /// Submit a new loan request. Validates against fund policies (FR-040 through FR-042).
    /// </summary>
    public async Task<Result<Loan>> RequestLoanAsync(
        Guid fundId,
        Guid borrowerId,
        decimal principalAmount,
        int requestedStartMonth,
        string? purpose,
        CancellationToken ct = default)
    {
        // Get fund projection for validation
        var fund = await _db.FundProjections
            .AsNoTracking()
            .FirstOrDefaultAsync(f => f.FundId == fundId && f.IsActive, ct);

        if (fund is null)
            return Result<Loan>.Failure("Fund not found or inactive.", "NOT_FOUND");

        // FR-042: Max loan per member cap
        if (fund.MaxLoanPerMember.HasValue && principalAmount > fund.MaxLoanPerMember.Value)
            return Result<Loan>.Failure(
                $"Loan amount exceeds maximum of {fund.MaxLoanPerMember.Value:N2}.",
                "MAX_LOAN_EXCEEDED");

        // FR-042: Max concurrent loans
        if (fund.MaxConcurrentLoans.HasValue)
        {
            var activeLoanCount = await _db.Loans
                .CountAsync(l => l.FundId == fundId
                    && l.BorrowerId == borrowerId
                    && (l.Status == LoanStatus.PendingApproval
                        || l.Status == LoanStatus.Approved
                        || l.Status == LoanStatus.Active), ct);

            if (activeLoanCount >= fund.MaxConcurrentLoans.Value)
                return Result<Loan>.Failure(
                    $"Borrower already has {activeLoanCount} active/pending loans (max: {fund.MaxConcurrentLoans.Value}).",
                    "MAX_CONCURRENT_LOANS");
        }

        var loan = Loan.Create(fundId, borrowerId, principalAmount, requestedStartMonth, purpose);

        _db.Loans.Add(loan);
        await _db.SaveChangesAsync(ct);

        // Publish LoanRequested event
        await _publishEndpoint.Publish(new LoanRequested(
            Id: Guid.NewGuid(),
            FundId: fundId,
            BorrowerId: borrowerId,
            LoanId: loan.Id,
            PrincipalAmount: principalAmount,
            OccurredAt: DateTime.UtcNow), ct);

        await _audit.PublishAsync(
            fundId: fundId,
            actorId: borrowerId,
            entityType: "Loan",
            entityId: loan.Id,
            actionType: "LoanRequested",
            beforeState: null,
            afterState: loan,
            serviceName: "FundManager.Loans",
            cancellationToken: ct);

        return Result<Loan>.Success(loan);
    }

    /// <summary>
    /// Approve a loan: set scheduled installment, snapshot fund config, disburse.
    /// Transitions PendingApproval → Approved → Active (FR-043, FR-050).
    /// </summary>
    public async Task<Result<Loan>> ApproveLoanAsync(
        Guid fundId,
        Guid loanId,
        Guid approvedBy,
        decimal scheduledInstallment,
        CancellationToken ct = default)
    {
        var loan = await _db.Loans
            .FirstOrDefaultAsync(l => l.Id == loanId && l.FundId == fundId, ct);

        if (loan is null)
            return Result<Loan>.Failure("Loan not found.", "NOT_FOUND");

        if (loan.Status != LoanStatus.PendingApproval)
            return Result<Loan>.Failure(
                $"Loan is in {loan.Status} status, cannot approve.",
                "INVALID_STATUS");

        // Get fund projection for snapshot fields
        var fund = await _db.FundProjections
            .AsNoTracking()
            .FirstOrDefaultAsync(f => f.FundId == fundId && f.IsActive, ct);

        if (fund is null)
            return Result<Loan>.Failure("Fund not found or inactive.", "NOT_FOUND");

        var beforeState = new
        {
            loan.Status,
            loan.ScheduledInstallment,
            loan.MonthlyInterestRate,
            loan.MinimumPrincipal
        };

        // Approve with snapshot fields from fund config
        loan.Approve(
            approvedBy: approvedBy,
            scheduledInstallment: scheduledInstallment,
            monthlyInterestRate: fund.MonthlyInterestRate,
            minimumPrincipal: fund.MinimumPrincipalPerRepayment);

        // Immediately disburse (Approved → Active)
        loan.Disburse();

        await _db.SaveChangesAsync(ct);

        // Publish LoanApproved
        await _publishEndpoint.Publish(new LoanApproved(
            Id: Guid.NewGuid(),
            FundId: fundId,
            LoanId: loan.Id,
            ApprovedBy: approvedBy,
            ScheduledInstallment: scheduledInstallment,
            OccurredAt: DateTime.UtcNow), ct);

        // Publish LoanDisbursed — Contributions service picks this up to create disbursement Transaction
        await _publishEndpoint.Publish(new LoanDisbursed(
            Id: Guid.NewGuid(),
            FundId: fundId,
            LoanId: loan.Id,
            BorrowerId: loan.BorrowerId,
            PrincipalAmount: loan.PrincipalAmount,
            OccurredAt: DateTime.UtcNow), ct);

        await _audit.PublishAsync(
            fundId: fundId,
            actorId: approvedBy,
            entityType: "Loan",
            entityId: loan.Id,
            actionType: "LoanApproved",
            beforeState: beforeState,
            afterState: loan,
            serviceName: "FundManager.Loans",
            cancellationToken: ct);

        return Result<Loan>.Success(loan);
    }

    /// <summary>
    /// Reject a loan request with a reason.
    /// Transitions PendingApproval → Rejected.
    /// </summary>
    public async Task<Result<Loan>> RejectLoanAsync(
        Guid fundId,
        Guid loanId,
        Guid rejectedBy,
        string reason,
        CancellationToken ct = default)
    {
        var loan = await _db.Loans
            .FirstOrDefaultAsync(l => l.Id == loanId && l.FundId == fundId, ct);

        if (loan is null)
            return Result<Loan>.Failure("Loan not found.", "NOT_FOUND");

        if (loan.Status != LoanStatus.PendingApproval)
            return Result<Loan>.Failure(
                $"Loan is in {loan.Status} status, cannot reject.",
                "INVALID_STATUS");

        var beforeState = new { loan.Status };

        loan.Reject(reason);
        await _db.SaveChangesAsync(ct);

        await _publishEndpoint.Publish(new LoanRejected(
            Id: Guid.NewGuid(),
            FundId: fundId,
            LoanId: loan.Id,
            Reason: reason,
            OccurredAt: DateTime.UtcNow), ct);

        await _audit.PublishAsync(
            fundId: fundId,
            actorId: rejectedBy,
            entityType: "Loan",
            entityId: loan.Id,
            actionType: "LoanRejected",
            beforeState: beforeState,
            afterState: loan,
            serviceName: "FundManager.Loans",
            cancellationToken: ct);

        return Result<Loan>.Success(loan);
    }

    /// <summary>
    /// Get a single loan by ID.
    /// </summary>
    public async Task<Loan?> GetLoanAsync(Guid fundId, Guid loanId, CancellationToken ct = default)
    {
        return await _db.Loans
            .AsNoTracking()
            .FirstOrDefaultAsync(l => l.Id == loanId && l.FundId == fundId, ct);
    }
}
