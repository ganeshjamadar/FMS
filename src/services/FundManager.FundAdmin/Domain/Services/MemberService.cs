using FundManager.BuildingBlocks.Audit;
using FundManager.BuildingBlocks.Domain;
using FundManager.Contracts.Events;
using FundManager.FundAdmin.Domain.Entities;
using FundManager.FundAdmin.Infrastructure.Data;
using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace FundManager.FundAdmin.Domain.Services;

public class MemberService
{
    private readonly FundAdminDbContext _db;
    private readonly IPublishEndpoint _publish;
    private readonly AuditEventPublisher _audit;

    public MemberService(
        FundAdminDbContext db,
        IPublishEndpoint publish,
        AuditEventPublisher audit)
    {
        _db = db;
        _publish = publish;
        _audit = audit;
    }

    /// <summary>
    /// Remove a member from a fund. FR-024: Cannot remove if outstanding loans/unpaid dues (cross-service).
    /// FR-015: Cannot remove the last Admin.
    /// </summary>
    public async Task<Result> RemoveMemberAsync(
        Guid fundId,
        Guid userId,
        Guid removedBy,
        CancellationToken ct = default)
    {
        var fund = await _db.Funds
            .Include(f => f.RoleAssignments)
            .Include(f => f.MemberPlans)
            .FirstOrDefaultAsync(f => f.Id == fundId, ct);

        if (fund is null)
            return Result.Failure("Fund not found.", "NOT_FOUND");

        var role = fund.RoleAssignments.FirstOrDefault(r => r.UserId == userId);
        if (role is null)
            return Result.Failure("User is not a member of this fund.", "NOT_MEMBER");

        // FR-015: Cannot remove the last admin
        if (role.Role == "Admin")
        {
            var adminCount = fund.RoleAssignments.Count(r => r.Role == "Admin");
            if (adminCount <= 1)
                return Result.Failure("Cannot remove the last Admin of the fund.", "LAST_ADMIN");
        }

        // TODO: FR-024 — Cross-service check for outstanding loans/unpaid contribution dues
        // This requires querying Contributions and Loans services via events or direct HTTP calls

        // Deactivate contribution plan
        var plan = fund.MemberPlans.FirstOrDefault(p => p.UserId == userId && p.IsActive);
        plan?.Deactivate();

        // Remove role assignment
        _db.FundRoleAssignments.Remove(role);

        await _db.SaveChangesAsync(ct);

        await _publish.Publish(new MemberRemoved(
            Id: Guid.NewGuid(),
            FundId: fundId,
            UserId: userId,
            OccurredAt: DateTime.UtcNow), ct);

        await _audit.PublishAsync(
            fundId: fundId,
            actorId: removedBy,
            entityType: "FundRoleAssignment",
            entityId: role.Id,
            actionType: "Member.Removed",
            beforeState: new { role.UserId, role.Role },
            afterState: null,
            serviceName: "FundAdmin",
            cancellationToken: ct);

        return Result.Success();
    }

    /// <summary>
    /// Change a member's role. FR-015: Cannot demote the last Admin.
    /// </summary>
    public async Task<Result> ChangeRoleAsync(
        Guid fundId,
        Guid userId,
        string newRole,
        Guid changedBy,
        CancellationToken ct = default)
    {
        var fund = await _db.Funds
            .Include(f => f.RoleAssignments)
            .FirstOrDefaultAsync(f => f.Id == fundId, ct);

        if (fund is null)
            return Result.Failure("Fund not found.", "NOT_FOUND");

        var assignment = fund.RoleAssignments.FirstOrDefault(r => r.UserId == userId);
        if (assignment is null)
            return Result.Failure("User is not a member of this fund.", "NOT_MEMBER");

        var oldRole = assignment.Role;

        // FR-015: Cannot demote the last admin
        if (oldRole == "Admin" && newRole != "Admin")
        {
            var adminCount = fund.RoleAssignments.Count(r => r.Role == "Admin");
            if (adminCount <= 1)
                return Result.Failure("Cannot demote the last Admin.", "LAST_ADMIN");
        }

        assignment.ChangeRole(newRole);
        await _db.SaveChangesAsync(ct);

        // Publish admin assigned event if promoted to Admin
        if (newRole == "Admin" && oldRole != "Admin")
        {
            await _publish.Publish(new FundAdminAssigned(
                Id: Guid.NewGuid(),
                FundId: fundId,
                UserId: userId,
                OccurredAt: DateTime.UtcNow), ct);
        }

        await _audit.PublishAsync(
            fundId: fundId,
            actorId: changedBy,
            entityType: "FundRoleAssignment",
            entityId: assignment.Id,
            actionType: "Member.RoleChanged",
            beforeState: new { OldRole = oldRole },
            afterState: new { NewRole = newRole },
            serviceName: "FundAdmin",
            cancellationToken: ct);

        return Result.Success();
    }
}
