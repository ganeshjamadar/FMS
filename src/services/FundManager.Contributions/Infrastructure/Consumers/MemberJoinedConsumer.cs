using FundManager.Contracts.Events;
using FundManager.Contributions.Domain.Entities;
using FundManager.Contributions.Infrastructure.Data;
using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace FundManager.Contributions.Infrastructure.Consumers;

/// <summary>
/// Maintains local MemberProjection when a member joins a fund.
/// Event-carried state transfer pattern — avoids cross-service queries for due generation.
/// </summary>
public class MemberJoinedConsumer : IConsumer<MemberJoined>
{
    private readonly ContributionsDbContext _db;

    public MemberJoinedConsumer(ContributionsDbContext db)
    {
        _db = db;
    }

    public async Task Consume(ConsumeContext<MemberJoined> context)
    {
        var msg = context.Message;

        // Idempotency: check if projection already exists
        var existing = await _db.MemberProjections
            .FirstOrDefaultAsync(m => m.UserId == msg.UserId && m.FundId == msg.FundId);

        if (existing is not null)
        {
            // Reactivate if previously deactivated
            if (!existing.IsActive)
            {
                // MemberProjection.Deactivate is the only state change; for reactivation
                // we'd need to handle it — for now, skip duplicates
            }
            return;
        }

        var projection = MemberProjection.Create(
            fundId: msg.FundId,
            userId: msg.UserId,
            memberPlanId: msg.MemberPlanId,
            monthlyContributionAmount: msg.MonthlyContributionAmount);

        _db.MemberProjections.Add(projection);
        await _db.SaveChangesAsync();
    }
}

/// <summary>
/// Deactivates local MemberProjection when a member is removed from a fund.
/// </summary>
public class MemberRemovedConsumer : IConsumer<MemberRemoved>
{
    private readonly ContributionsDbContext _db;

    public MemberRemovedConsumer(ContributionsDbContext db)
    {
        _db = db;
    }

    public async Task Consume(ConsumeContext<MemberRemoved> context)
    {
        var msg = context.Message;

        var projection = await _db.MemberProjections
            .FirstOrDefaultAsync(m => m.UserId == msg.UserId && m.FundId == msg.FundId);

        if (projection is not null)
        {
            projection.Deactivate();
            await _db.SaveChangesAsync();
        }
    }
}
