using FundManager.Contracts.Events;
using FundManager.Loans.Domain.Entities;
using FundManager.Loans.Infrastructure.Data;
using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace FundManager.Loans.Infrastructure.Jobs;

/// <summary>
/// Background service that detects overdue repayments and sends reminders.
/// FR-070: Mark as Overdue if not fully paid by due date.
/// FR-071: Send reminders at 3, 7, 14 days past due.
/// Runs every hour.
/// </summary>
public class RepaymentOverdueJob : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromHours(1);
    private static readonly int[] ReminderDays = [3, 7, 14];

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<RepaymentOverdueJob> _logger;

    public RepaymentOverdueJob(
        IServiceScopeFactory scopeFactory,
        ILogger<RepaymentOverdueJob> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("RepaymentOverdueJob started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunDetectionCycleAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Error during repayment overdue detection cycle");
            }

            await Task.Delay(Interval, stoppingToken);
        }
    }

    private async Task RunDetectionCycleAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LoansDbContext>();
        var publish = scope.ServiceProvider.GetRequiredService<IPublishEndpoint>();

        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        // Find all repayment entries that are past due and not fully paid
        var overdueEntries = await db.RepaymentEntries
            .Where(r => r.DueDate < today
                        && r.Status != RepaymentStatus.Paid)
            .ToListAsync(ct);

        if (overdueEntries.Count == 0) return;

        _logger.LogInformation("RepaymentOverdueJob found {Count} overdue entries", overdueEntries.Count);

        // Group by loan to look up borrower
        var loanIds = overdueEntries.Select(r => r.LoanId).Distinct().ToList();
        var loans = await db.Loans
            .Where(l => loanIds.Contains(l.Id))
            .ToDictionaryAsync(l => l.Id, ct);

        var changed = false;

        foreach (var entry in overdueEntries)
        {
            var daysPastDue = today.DayNumber - entry.DueDate.DayNumber;

            // FR-070: Mark as Overdue
            if (entry.Status != RepaymentStatus.Overdue)
            {
                entry.MarkOverdue();
                changed = true;
            }

            // FR-071: Publish overdue event at reminder intervals (3, 7, 14 days)
            if (ReminderDays.Contains(daysPastDue))
            {
                var borrowerId = loans.TryGetValue(entry.LoanId, out var loan)
                    ? loan.BorrowerId
                    : Guid.Empty;

                await publish.Publish(new RepaymentOverdue(
                    Id: Guid.NewGuid(),
                    FundId: entry.FundId,
                    LoanId: entry.LoanId,
                    RepaymentEntryId: entry.Id,
                    BorrowerId: borrowerId,
                    MonthYear: entry.MonthYear,
                    AmountDue: entry.TotalDue,
                    AmountPaid: entry.AmountPaid,
                    DaysPastDue: daysPastDue,
                    OccurredAt: DateTime.UtcNow), ct);

                _logger.LogInformation(
                    "Sent overdue reminder for loan {LoanId}, entry {EntryId}, {Days} days past due",
                    entry.LoanId, entry.Id, daysPastDue);
            }
        }

        if (changed)
            await db.SaveChangesAsync(ct);
    }
}
