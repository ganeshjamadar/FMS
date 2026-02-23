using FundManager.BuildingBlocks.Audit;
using FundManager.BuildingBlocks.Domain;
using FundManager.Contracts.Events;
using FundManager.FundAdmin.Api.Controllers;
using FundManager.FundAdmin.Domain.Entities;
using FundManager.FundAdmin.Infrastructure.Data;
using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace FundManager.FundAdmin.Domain.Services;

/// <summary>
/// Fund lifecycle operations: create, activate, update, assign admin.
/// </summary>
public class FundService
{
    private readonly FundAdminDbContext _dbContext;
    private readonly IPublishEndpoint _publishEndpoint;
    private readonly AuditEventPublisher _auditPublisher;

    public FundService(
        FundAdminDbContext dbContext,
        IPublishEndpoint publishEndpoint,
        AuditEventPublisher auditPublisher)
    {
        _dbContext = dbContext;
        _publishEndpoint = publishEndpoint;
        _auditPublisher = auditPublisher;
    }

    /// <summary>
    /// Create a new fund in Draft state with all configuration.
    /// All config fields (except description) are immutable after creation (FR-011).
    /// </summary>
    public async Task<Result<Fund>> CreateFundAsync(
        string name,
        decimal monthlyInterestRate,
        decimal minimumMonthlyContribution,
        decimal minimumPrincipalPerRepayment,
        Guid creatorId,
        string? description = null,
        string currency = "INR",
        string loanApprovalPolicy = "AdminOnly",
        decimal? maxLoanPerMember = null,
        int? maxConcurrentLoans = null,
        string? dissolutionPolicy = null,
        string overduePenaltyType = "None",
        decimal overduePenaltyValue = 0.00m,
        int contributionDayOfMonth = 1,
        int gracePeriodDays = 5,
        CancellationToken ct = default)
    {
        try
        {
            var fund = Fund.Create(
                name: name,
                monthlyInterestRate: monthlyInterestRate,
                minimumMonthlyContribution: minimumMonthlyContribution,
                minimumPrincipalPerRepayment: minimumPrincipalPerRepayment,
                description: description,
                currency: currency,
                loanApprovalPolicy: loanApprovalPolicy,
                maxLoanPerMember: maxLoanPerMember,
                maxConcurrentLoans: maxConcurrentLoans,
                dissolutionPolicy: dissolutionPolicy,
                overduePenaltyType: overduePenaltyType,
                overduePenaltyValue: overduePenaltyValue,
                contributionDayOfMonth: contributionDayOfMonth,
                gracePeriodDays: gracePeriodDays);

            _dbContext.Funds.Add(fund);
            await _dbContext.SaveChangesAsync(ct);

            // Publish integration event
            await _publishEndpoint.Publish(new FundCreated(
                Id: Guid.NewGuid(),
                FundId: fund.Id,
                Name: fund.Name,
                Currency: fund.Currency,
                MonthlyInterestRate: fund.MonthlyInterestRate,
                MinimumMonthlyContribution: fund.MinimumMonthlyContribution,
                MinimumPrincipalPerRepayment: fund.MinimumPrincipalPerRepayment,
                LoanApprovalPolicy: fund.LoanApprovalPolicy,
                MaxLoanPerMember: fund.MaxLoanPerMember,
                MaxConcurrentLoans: fund.MaxConcurrentLoans,
                OccurredAt: DateTime.UtcNow), ct);

            // Publish audit event
            await _auditPublisher.PublishAsync(
                fundId: fund.Id,
                actorId: creatorId,
                entityType: "Fund",
                entityId: fund.Id,
                actionType: "Fund.Created",
                beforeState: null,
                afterState: fund,
                serviceName: "FundAdmin",
                cancellationToken: ct);

            return Result<Fund>.Success(fund);
        }
        catch (ArgumentException ex)
        {
            return Result<Fund>.Failure(ex.Message, "VALIDATION_ERROR");
        }
    }

    /// <summary>
    /// Activate a fund (Draft → Active). Requires ≥ 1 Admin assigned (FR-015).
    /// </summary>
    public async Task<Result<Fund>> ActivateFundAsync(Guid fundId, Guid activatorId, CancellationToken ct = default)
    {
        var fund = await _dbContext.Funds
            .Include(f => f.RoleAssignments)
            .FirstOrDefaultAsync(f => f.Id == fundId, ct);

        if (fund is null)
            return Result<Fund>.Failure("Fund not found.", "NOT_FOUND");

        var beforeState = new { fund.State };
        var result = fund.Activate();
        if (!result.IsSuccess)
            return Result<Fund>.Failure(result.Error!, result.ErrorCode);

        await _dbContext.SaveChangesAsync(ct);

        // Publish integration event
        await _publishEndpoint.Publish(new FundActivated(
            Id: Guid.NewGuid(),
            FundId: fund.Id,
            OccurredAt: DateTime.UtcNow), ct);

        // Audit
        await _auditPublisher.PublishAsync(
            fundId: fund.Id,
            actorId: activatorId,
            entityType: "Fund",
            entityId: fund.Id,
            actionType: "Fund.Activated",
            beforeState: beforeState,
            afterState: new { fund.State },
            serviceName: "FundAdmin",
            cancellationToken: ct);

        return Result<Fund>.Success(fund);
    }

    /// <summary>
    /// Update fund fields. Description is always updatable.
    /// All other config fields are only updatable while the fund is in Draft state (FR-011).
    /// </summary>
    public async Task<Result<Fund>> UpdateFundAsync(
        Guid fundId, UpdateFundRequestDto request, Guid updaterId, CancellationToken ct = default)
    {
        var fund = await _dbContext.Funds.FirstOrDefaultAsync(f => f.Id == fundId, ct);
        if (fund is null)
            return Result<Fund>.Failure("Fund not found.", "NOT_FOUND");

        var beforeState = new
        {
            fund.Name, fund.Description, fund.Currency, fund.MonthlyInterestRate,
            fund.MinimumMonthlyContribution, fund.MinimumPrincipalPerRepayment,
            fund.LoanApprovalPolicy, fund.MaxLoanPerMember, fund.MaxConcurrentLoans,
            fund.DissolutionPolicy, fund.OverduePenaltyType, fund.OverduePenaltyValue,
            fund.ContributionDayOfMonth, fund.GracePeriodDays
        };

        // Description is always updatable regardless of state
        if (request.Description is not null || request.Description != fund.Description)
            fund.UpdateDescription(request.Description);

        // Check if any config fields are being updated (anything beyond description)
        bool hasConfigUpdates = request.Name is not null
            || request.MonthlyInterestRate.HasValue
            || request.MinimumMonthlyContribution.HasValue
            || request.MinimumPrincipalPerRepayment.HasValue
            || request.Currency is not null
            || request.LoanApprovalPolicy is not null
            || request.MaxLoanPerMember.HasValue || request.ClearMaxLoanPerMember
            || request.MaxConcurrentLoans.HasValue || request.ClearMaxConcurrentLoans
            || request.DissolutionPolicy is not null
            || request.OverduePenaltyType is not null
            || request.OverduePenaltyValue.HasValue
            || request.ContributionDayOfMonth.HasValue
            || request.GracePeriodDays.HasValue;

        if (hasConfigUpdates)
        {
            var configResult = fund.UpdateConfiguration(
                name: request.Name,
                monthlyInterestRate: request.MonthlyInterestRate,
                minimumMonthlyContribution: request.MinimumMonthlyContribution,
                minimumPrincipalPerRepayment: request.MinimumPrincipalPerRepayment,
                currency: request.Currency,
                loanApprovalPolicy: request.LoanApprovalPolicy,
                maxLoanPerMember: request.MaxLoanPerMember,
                clearMaxLoanPerMember: request.ClearMaxLoanPerMember,
                maxConcurrentLoans: request.MaxConcurrentLoans,
                clearMaxConcurrentLoans: request.ClearMaxConcurrentLoans,
                dissolutionPolicy: request.DissolutionPolicy,
                overduePenaltyType: request.OverduePenaltyType,
                overduePenaltyValue: request.OverduePenaltyValue,
                contributionDayOfMonth: request.ContributionDayOfMonth,
                gracePeriodDays: request.GracePeriodDays);

            if (!configResult.IsSuccess)
                return Result<Fund>.Failure(configResult.Error!, configResult.ErrorCode);
        }

        await _dbContext.SaveChangesAsync(ct);

        var afterState = new
        {
            fund.Name, fund.Description, fund.Currency, fund.MonthlyInterestRate,
            fund.MinimumMonthlyContribution, fund.MinimumPrincipalPerRepayment,
            fund.LoanApprovalPolicy, fund.MaxLoanPerMember, fund.MaxConcurrentLoans,
            fund.DissolutionPolicy, fund.OverduePenaltyType, fund.OverduePenaltyValue,
            fund.ContributionDayOfMonth, fund.GracePeriodDays
        };

        await _auditPublisher.PublishAsync(
            fundId: fund.Id,
            actorId: updaterId,
            entityType: "Fund",
            entityId: fund.Id,
            actionType: "Fund.Updated",
            beforeState: beforeState,
            afterState: afterState,
            serviceName: "FundAdmin",
            cancellationToken: ct);

        return Result<Fund>.Success(fund);
    }

    /// <summary>
    /// Assign a Fund Admin role to a user. Publishes FundAdminAssigned event.
    /// </summary>
    public async Task<Result<FundRoleAssignment>> AssignRoleAsync(
        Guid fundId, Guid userId, string role, Guid assignedBy, CancellationToken ct = default)
    {
        var fund = await _dbContext.Funds
            .Include(f => f.RoleAssignments)
            .FirstOrDefaultAsync(f => f.Id == fundId, ct);

        if (fund is null)
            return Result<FundRoleAssignment>.Failure("Fund not found.", "NOT_FOUND");

        var result = fund.AssignRole(userId, role, assignedBy);
        if (!result.IsSuccess)
            return result;

        await _dbContext.SaveChangesAsync(ct);

        // Publish FundAdminAssigned if role is Admin
        if (role == "Admin")
        {
            await _publishEndpoint.Publish(new FundAdminAssigned(
                Id: Guid.NewGuid(),
                FundId: fundId,
                UserId: userId,
                OccurredAt: DateTime.UtcNow), ct);
        }

        await _auditPublisher.PublishAsync(
            fundId: fundId,
            actorId: assignedBy,
            entityType: "FundRoleAssignment",
            entityId: result.Value!.Id,
            actionType: "FundRoleAssignment.Created",
            beforeState: null,
            afterState: new { result.Value!.UserId, result.Value.FundId, result.Value.Role },
            serviceName: "FundAdmin",
            cancellationToken: ct);

        return result;
    }

    /// <summary>
    /// Change a member's role. Cannot demote last Admin (FR-015).
    /// </summary>
    public async Task<Result> ChangeRoleAsync(
        Guid fundId, Guid userId, string newRole, Guid changedBy, CancellationToken ct = default)
    {
        var fund = await _dbContext.Funds
            .Include(f => f.RoleAssignments)
            .FirstOrDefaultAsync(f => f.Id == fundId, ct);

        if (fund is null)
            return Result.Failure("Fund not found.", "NOT_FOUND");

        var assignment = fund.RoleAssignments.FirstOrDefault(r => r.UserId == userId);
        var beforeRole = assignment?.Role;

        var result = fund.ChangeRole(userId, newRole);
        if (!result.IsSuccess)
            return result;

        await _dbContext.SaveChangesAsync(ct);

        await _auditPublisher.PublishAsync(
            fundId: fundId,
            actorId: changedBy,
            entityType: "FundRoleAssignment",
            entityId: assignment!.Id,
            actionType: "FundRoleAssignment.RoleChanged",
            beforeState: new { Role = beforeRole },
            afterState: new { Role = newRole },
            serviceName: "FundAdmin",
            cancellationToken: ct);

        return result;
    }

    /// <summary>
    /// Remove a member from the fund.
    /// Blocked if member has outstanding obligations (FR-024) — cross-service check needed.
    /// </summary>
    public async Task<Result> RemoveMemberAsync(
        Guid fundId, Guid userId, Guid removedBy, CancellationToken ct = default)
    {
        var fund = await _dbContext.Funds
            .Include(f => f.RoleAssignments)
            .Include(f => f.MemberPlans)
            .FirstOrDefaultAsync(f => f.Id == fundId, ct);

        if (fund is null)
            return Result.Failure("Fund not found.", "NOT_FOUND");

        var assignment = fund.RoleAssignments.FirstOrDefault(r => r.UserId == userId);
        if (assignment is null)
            return Result.Failure("User not found in this fund.", "USER_NOT_FOUND");

        // Cannot remove the last Admin
        if (assignment.Role == "Admin" && fund.RoleAssignments.Count(r => r.Role == "Admin") <= 1)
            return Result.Failure("Cannot remove the last Admin (FR-015).", "LAST_ADMIN");

        // TODO: Cross-service check for outstanding loans/dues (FR-024)

        // Remove role assignment
        _dbContext.Set<FundRoleAssignment>().Remove(assignment);

        // Deactivate member contribution plan if exists
        var plan = fund.MemberPlans.FirstOrDefault(p => p.UserId == userId);
        plan?.Deactivate();

        await _dbContext.SaveChangesAsync(ct);

        // Publish MemberRemoved event
        await _publishEndpoint.Publish(new MemberRemoved(
            Id: Guid.NewGuid(),
            FundId: fundId,
            UserId: userId,
            OccurredAt: DateTime.UtcNow), ct);

        await _auditPublisher.PublishAsync(
            fundId: fundId,
            actorId: removedBy,
            entityType: "FundRoleAssignment",
            entityId: assignment.Id,
            actionType: "FundRoleAssignment.Removed",
            beforeState: new { assignment.UserId, assignment.FundId, assignment.Role },
            afterState: null,
            serviceName: "FundAdmin",
            cancellationToken: ct);

        return Result.Success();
    }

    /// <summary>
    /// Get the fund dashboard summary.
    /// </summary>
    public async Task<Fund?> GetFundAsync(Guid fundId, CancellationToken ct = default)
    {
        return await _dbContext.Funds
            .Include(f => f.RoleAssignments)
            .Include(f => f.MemberPlans)
            .AsNoTracking()
            .FirstOrDefaultAsync(f => f.Id == fundId, ct);
    }
}
