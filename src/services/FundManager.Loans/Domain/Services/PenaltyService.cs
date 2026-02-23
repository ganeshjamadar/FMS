using FundManager.BuildingBlocks.Audit;
using FundManager.Contracts.Events;
using FundManager.Loans.Domain.Entities;
using FundManager.Loans.Infrastructure.Data;
using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace FundManager.Loans.Domain.Services;

/// <summary>
/// Calculates and applies penalties on overdue repayment entries.
/// FR-072: Penalty defaults to none; Admin may configure flat or percentage.
/// FR-073: If penalty configured, adds amount to next month's RepaymentEntry.
/// </summary>
public class PenaltyService
{
    private readonly LoansDbContext _db;
    private readonly IPublishEndpoint _publish;
    private readonly AuditEventPublisher _audit;
    private readonly ILogger<PenaltyService> _logger;

    public PenaltyService(
        LoansDbContext db,
        IPublishEndpoint publish,
        AuditEventPublisher audit,
        ILogger<PenaltyService> logger)
    {
        _db = db;
        _publish = publish;
        _audit = audit;
        _logger = logger;
    }

    /// <summary>
    /// Apply penalties for all overdue entries in a fund.
    /// Returns number of entries penalised.
    /// </summary>
    public async Task<int> ApplyPenaltiesAsync(Guid fundId, CancellationToken ct = default)
    {
        // Look up fund's penalty configuration
        var fundConfig = await _db.FundProjections
            .FirstOrDefaultAsync(f => f.FundId == fundId, ct);

        if (fundConfig is null || fundConfig.PenaltyType == "None" || fundConfig.PenaltyValue <= 0)
            return 0;

        // Find overdue entries that haven't been penalised yet
        // We track penalised entries by checking if a penalty has already been
        // applied in the next month's entry.
        var overdueEntries = await _db.RepaymentEntries
            .Where(r => r.FundId == fundId && r.Status == RepaymentStatus.Overdue)
            .ToListAsync(ct);

        if (overdueEntries.Count == 0) return 0;

        var penaltyCount = 0;

        foreach (var entry in overdueEntries)
        {
            var overdueAmount = entry.TotalDue - entry.AmountPaid;
            if (overdueAmount <= 0) continue;

            // Calculate penalty
            var penaltyAmount = CalculatePenalty(fundConfig.PenaltyType, fundConfig.PenaltyValue, overdueAmount);
            if (penaltyAmount <= 0) continue;

            // FR-073: Find or create next month's entry and add penalty
            var nextMonth = GetNextMonthYear(entry.MonthYear);
            var nextEntry = await _db.RepaymentEntries
                .FirstOrDefaultAsync(r => r.LoanId == entry.LoanId && r.MonthYear == nextMonth, ct);

            if (nextEntry is not null)
            {
                // Add penalty to existing entry's total
                nextEntry.AddPenalty(penaltyAmount);
            }
            else
            {
                // Create a penalty-only entry for next month
                var dueDate = new DateOnly(nextMonth / 100, nextMonth % 100, 15);
                var penaltyEntry = RepaymentEntry.Create(
                    entry.LoanId, fundId, nextMonth,
                    interestDue: 0, principalDue: 0, totalDue: penaltyAmount,
                    dueDate: dueDate);
                _db.RepaymentEntries.Add(penaltyEntry);
            }

            // Publish event
            await _publish.Publish(new RepaymentPenaltyApplied(
                Id: Guid.NewGuid(),
                FundId: fundId,
                LoanId: entry.LoanId,
                RepaymentEntryId: entry.Id,
                PenaltyAmount: penaltyAmount,
                PenaltyType: fundConfig.PenaltyType,
                OccurredAt: DateTime.UtcNow), ct);

            penaltyCount++;

            _logger.LogInformation(
                "Applied {PenaltyType} penalty of {Amount} for loan {LoanId}, overdue entry {EntryId}",
                fundConfig.PenaltyType, penaltyAmount, entry.LoanId, entry.Id);
        }

        if (penaltyCount > 0)
            await _db.SaveChangesAsync(ct);

        return penaltyCount;
    }

    /// <summary>
    /// Calculate penalty amount based on type and overdue balance.
    /// </summary>
    private static decimal CalculatePenalty(string penaltyType, decimal penaltyValue, decimal overdueAmount)
    {
        return penaltyType switch
        {
            "Flat" => Math.Round(penaltyValue, 2, MidpointRounding.ToEven),
            "Percentage" => Math.Round(overdueAmount * penaltyValue / 100m, 2, MidpointRounding.ToEven),
            _ => 0m
        };
    }

    /// <summary>
    /// Get next month YYYYMM from current YYYYMM.
    /// </summary>
    private static int GetNextMonthYear(int monthYear)
    {
        var year = monthYear / 100;
        var month = monthYear % 100;
        if (month == 12)
            return (year + 1) * 100 + 1;
        return year * 100 + month + 1;
    }
}
