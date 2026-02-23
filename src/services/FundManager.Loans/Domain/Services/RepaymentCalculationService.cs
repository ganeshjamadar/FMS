using FundManager.BuildingBlocks.Domain;
using FundManager.BuildingBlocks.Financial;
using FundManager.Contracts.Events;
using FundManager.Loans.Domain.Entities;
using FundManager.Loans.Infrastructure.Data;
using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace FundManager.Loans.Domain.Services;

/// <summary>
/// Generates monthly repayment entries using reducing-balance formula.
/// Interest = OutstandingPrincipal × MonthlyInterestRate (FR-060)
/// PrincipalDue = MAX(MinimumPrincipal, ScheduledInstallment - InterestDue), capped at OutstandingPrincipal (FR-061–FR-063)
/// Idempotent: re-running for same (loanId, monthYear) returns existing entry.
/// </summary>
public class RepaymentCalculationService
{
    private readonly LoansDbContext _db;
    private readonly IPublishEndpoint _publishEndpoint;

    public RepaymentCalculationService(
        LoansDbContext db,
        IPublishEndpoint publishEndpoint)
    {
        _db = db;
        _publishEndpoint = publishEndpoint;
    }

    /// <summary>
    /// Generate a repayment entry for the specified loan and month.
    /// Idempotent — if entry already exists for (loanId, monthYear), return it.
    /// </summary>
    public async Task<Result<RepaymentEntry>> GenerateRepaymentAsync(
        Guid fundId,
        Guid loanId,
        int monthYear,
        CancellationToken ct = default)
    {
        // Validate monthYear format (YYYYMM)
        var year = monthYear / 100;
        var month = monthYear % 100;
        if (year < 2000 || year > 2100 || month < 1 || month > 12)
            return Result<RepaymentEntry>.Failure("Invalid monthYear format. Expected YYYYMM.", "VALIDATION");

        // Get the loan
        var loan = await _db.Loans
            .FirstOrDefaultAsync(l => l.Id == loanId && l.FundId == fundId, ct);

        if (loan is null)
            return Result<RepaymentEntry>.Failure("Loan not found.", "NOT_FOUND");

        if (loan.Status != LoanStatus.Active)
            return Result<RepaymentEntry>.Failure(
                $"Cannot generate repayment for loan in {loan.Status} status.",
                "INVALID_STATUS");

        // Idempotency: check if entry already exists
        var existing = await _db.RepaymentEntries
            .FirstOrDefaultAsync(r => r.LoanId == loanId && r.MonthYear == monthYear, ct);

        if (existing is not null)
            return Result<RepaymentEntry>.Success(existing);

        // Calculate using MoneyMath (reducing-balance formula)
        var interestDue = MoneyMath.CalculateMonthlyInterest(
            loan.OutstandingPrincipal, loan.MonthlyInterestRate);

        var principalDue = MoneyMath.CalculatePrincipalDue(
            loan.OutstandingPrincipal, loan.MinimumPrincipal,
            loan.ScheduledInstallment, interestDue);

        var totalDue = MoneyMath.CalculateTotalDue(interestDue, principalDue);

        // Due date: last day of the month
        var dueDate = new DateOnly(year, month, DateTime.DaysInMonth(year, month));

        var entry = RepaymentEntry.Create(
            loanId: loanId,
            fundId: fundId,
            monthYear: monthYear,
            interestDue: interestDue,
            principalDue: principalDue,
            totalDue: totalDue,
            dueDate: dueDate);

        _db.RepaymentEntries.Add(entry);
        await _db.SaveChangesAsync(ct);

        // Publish RepaymentDueGenerated event
        await _publishEndpoint.Publish(new RepaymentDueGenerated(
            Id: Guid.NewGuid(),
            FundId: fundId,
            LoanId: loanId,
            RepaymentEntryId: entry.Id,
            MonthYear: monthYear,
            InterestDue: interestDue,
            PrincipalDue: principalDue,
            TotalDue: totalDue,
            OccurredAt: DateTime.UtcNow), ct);

        return Result<RepaymentEntry>.Success(entry);
    }

    /// <summary>
    /// List all repayment entries for a loan, ordered by month.
    /// </summary>
    public async Task<List<RepaymentEntry>> ListRepaymentsAsync(
        Guid fundId,
        Guid loanId,
        CancellationToken ct = default)
    {
        return await _db.RepaymentEntries
            .AsNoTracking()
            .Where(r => r.LoanId == loanId && r.FundId == fundId)
            .OrderBy(r => r.MonthYear)
            .ToListAsync(ct);
    }
}
