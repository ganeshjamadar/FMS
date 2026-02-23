using FundManager.BuildingBlocks.Audit;
using FundManager.BuildingBlocks.Domain;
using FundManager.Contracts.Events;
using FundManager.Contributions.Domain.Entities;
using FundManager.Contributions.Infrastructure.Data;
using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace FundManager.Contributions.Domain.Services;

/// <summary>
/// Detects and marks overdue contributions.
/// FR-033: Pending → Late after grace period.
/// FR-034: Late → Missed at month-end.
/// </summary>
public class OverdueDetectionService
{
    private readonly ContributionsDbContext _db;
    private readonly IPublishEndpoint _publish;
    private readonly AuditEventPublisher _audit;

    public OverdueDetectionService(
        ContributionsDbContext db,
        IPublishEndpoint publish,
        AuditEventPublisher audit)
    {
        _db = db;
        _publish = publish;
        _audit = audit;
    }

    /// <summary>
    /// FR-033: Mark Pending dues as Late after the grace period (e.g., 5 days past due date).
    /// </summary>
    public async Task<int> MarkLateAsync(
        Guid fundId,
        int graceDays,
        CancellationToken ct = default)
    {
        var cutoff = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-graceDays);

        var pendingDues = await _db.ContributionDues
            .Where(d => d.FundId == fundId
                        && d.Status == ContributionDueStatus.Pending
                        && d.DueDate <= cutoff)
            .ToListAsync(ct);

        foreach (var due in pendingDues)
        {
            due.MarkLate();

            await _publish.Publish(new ContributionOverdue(
                Id: Guid.NewGuid(),
                FundId: fundId,
                UserId: due.UserId,
                ContributionDueId: due.Id,
                MonthYear: due.MonthYear,
                Status: "Late",
                OccurredAt: DateTime.UtcNow), ct);
        }

        if (pendingDues.Count > 0)
            await _db.SaveChangesAsync(ct);

        return pendingDues.Count;
    }

    /// <summary>
    /// FR-034: Mark Late/Partial dues as Missed at month-end.
    /// </summary>
    public async Task<int> MarkMissedAsync(
        Guid fundId,
        int monthYear,
        CancellationToken ct = default)
    {
        var overdueDues = await _db.ContributionDues
            .Where(d => d.FundId == fundId
                        && d.MonthYear == monthYear
                        && (d.Status == ContributionDueStatus.Late
                            || d.Status == ContributionDueStatus.Pending
                            || d.Status == ContributionDueStatus.Partial))
            .ToListAsync(ct);

        foreach (var due in overdueDues)
        {
            due.MarkMissed();

            await _publish.Publish(new ContributionOverdue(
                Id: Guid.NewGuid(),
                FundId: fundId,
                UserId: due.UserId,
                ContributionDueId: due.Id,
                MonthYear: due.MonthYear,
                Status: "Missed",
                OccurredAt: DateTime.UtcNow), ct);
        }

        if (overdueDues.Count > 0)
            await _db.SaveChangesAsync(ct);

        return overdueDues.Count;
    }
}
