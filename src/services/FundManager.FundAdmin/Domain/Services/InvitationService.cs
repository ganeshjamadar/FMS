using FundManager.BuildingBlocks.Audit;
using FundManager.BuildingBlocks.Domain;
using FundManager.Contracts.Events;
using FundManager.FundAdmin.Domain.Entities;
using FundManager.FundAdmin.Infrastructure.Data;
using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace FundManager.FundAdmin.Domain.Services;

public class InvitationService
{
    private readonly FundAdminDbContext _db;
    private readonly IPublishEndpoint _publish;
    private readonly AuditEventPublisher _audit;

    public InvitationService(
        FundAdminDbContext db,
        IPublishEndpoint publish,
        AuditEventPublisher audit)
    {
        _db = db;
        _publish = publish;
        _audit = audit;
    }

    /// <summary>
    /// Invite a user (by phone/email) to a fund. FR-020: Only Active funds accept invitations.
    /// </summary>
    public async Task<Result<Invitation>> InviteAsync(
        Guid fundId,
        string targetContact,
        Guid invitedBy,
        CancellationToken ct = default)
    {
        var fund = await _db.Funds.FirstOrDefaultAsync(f => f.Id == fundId, ct);
        if (fund is null)
            return Result<Invitation>.Failure("Fund not found.", "NOT_FOUND");
        if (fund.State != FundState.Active)
            return Result<Invitation>.Failure("Only active funds can accept new members.", "INVALID_STATE");

        // Check for existing pending invitation to same contact in same fund
        var existingPending = await _db.Invitations.AnyAsync(
            i => i.FundId == fundId
                 && i.TargetContact == targetContact.Trim()
                 && i.Status == InvitationStatus.Pending
                 && i.ExpiresAt > DateTime.UtcNow,
            ct);
        if (existingPending)
            return Result<Invitation>.Failure("A pending invitation already exists for this contact.", "DUPLICATE");

        var invitation = Invitation.Create(fundId, targetContact, invitedBy);
        _db.Invitations.Add(invitation);
        await _db.SaveChangesAsync(ct);

        await _publish.Publish(new InvitationSent(
            Id: Guid.NewGuid(),
            FundId: fundId,
            InvitationId: invitation.Id,
            TargetContact: targetContact,
            OccurredAt: DateTime.UtcNow), ct);

        await _audit.PublishAsync(
            fundId: fundId,
            actorId: invitedBy,
            entityType: "Invitation",
            entityId: invitation.Id,
            actionType: "Invitation.Created",
            beforeState: null,
            afterState: invitation,
            serviceName: "FundAdmin",
            cancellationToken: ct);

        return Result<Invitation>.Success(invitation);
    }

    /// <summary>
    /// Accept an invitation and create membership. FR-021, FR-022, FR-023.
    /// </summary>
    public async Task<Result> AcceptAsync(
        Guid invitationId,
        Guid acceptingUserId,
        decimal monthlyContributionAmount,
        CancellationToken ct = default)
    {
        var invitation = await _db.Invitations
            .Include(i => i.Fund)
            .FirstOrDefaultAsync(i => i.Id == invitationId, ct);

        if (invitation is null)
            return Result.Failure("Invitation not found.", "NOT_FOUND");

        // Validate contribution amount >= fund minimum (FR-023)
        if (monthlyContributionAmount < invitation.Fund.MinimumMonthlyContribution)
            return Result.Failure(
                $"Contribution amount must be at least {invitation.Fund.MinimumMonthlyContribution}.",
                "BELOW_MINIMUM");

        var acceptResult = invitation.Accept();
        if (!acceptResult.IsSuccess)
            return acceptResult;

        // Check if user is already a member
        var alreadyMember = await _db.MemberContributionPlans.AnyAsync(
            p => p.UserId == acceptingUserId && p.FundId == invitation.FundId && p.IsActive,
            ct);
        if (alreadyMember)
            return Result.Failure("User is already an active member of this fund.", "ALREADY_MEMBER");

        // Create role assignment (default: Editor)
        var role = FundRoleAssignment.Create(
            acceptingUserId,
            invitation.FundId,
            "Editor",
            invitation.InvitedBy);
        _db.FundRoleAssignments.Add(role);

        // Create contribution plan (FR-023: immutable amount)
        var plan = MemberContributionPlan.Create(
            acceptingUserId,
            invitation.FundId,
            monthlyContributionAmount);
        _db.MemberContributionPlans.Add(plan);

        await _db.SaveChangesAsync(ct);

        await _publish.Publish(new MemberJoined(
            Id: Guid.NewGuid(),
            FundId: invitation.FundId,
            UserId: acceptingUserId,
            MemberPlanId: plan.Id,
            MonthlyContributionAmount: monthlyContributionAmount,
            OccurredAt: DateTime.UtcNow), ct);

        await _audit.PublishAsync(
            fundId: invitation.FundId,
            actorId: acceptingUserId,
            entityType: "Invitation",
            entityId: invitation.Id,
            actionType: "Invitation.Accepted",
            beforeState: null,
            afterState: new { invitation.FundId, MonthlyContributionAmount = monthlyContributionAmount },
            serviceName: "FundAdmin",
            cancellationToken: ct);

        return Result.Success();
    }

    /// <summary>
    /// Decline an invitation. FR-022.
    /// </summary>
    public async Task<Result> DeclineAsync(
        Guid invitationId,
        Guid decliningUserId,
        CancellationToken ct = default)
    {
        var invitation = await _db.Invitations
            .FirstOrDefaultAsync(i => i.Id == invitationId, ct);

        if (invitation is null)
            return Result.Failure("Invitation not found.", "NOT_FOUND");

        var declineResult = invitation.Decline();
        if (!declineResult.IsSuccess)
            return declineResult;

        await _db.SaveChangesAsync(ct);

        await _audit.PublishAsync(
            fundId: invitation.FundId,
            actorId: decliningUserId,
            entityType: "Invitation",
            entityId: invitation.Id,
            actionType: "Invitation.Declined",
            beforeState: null,
            afterState: null,
            serviceName: "FundAdmin",
            cancellationToken: ct);

        return Result.Success();
    }

    /// <summary>
    /// List invitations for a fund with optional status filter.
    /// </summary>
    public async Task<(List<Invitation> Items, int TotalCount)> ListAsync(
        Guid fundId,
        InvitationStatus? status,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        var query = _db.Invitations
            .AsNoTracking()
            .Where(i => i.FundId == fundId);

        if (status.HasValue)
            query = query.Where(i => i.Status == status.Value);

        var totalCount = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(i => i.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (items, totalCount);
    }
}
