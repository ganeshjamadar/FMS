using System.Security.Claims;
using FundManager.FundAdmin.Domain.Entities;
using FundManager.FundAdmin.Domain.Services;
using FundManager.FundAdmin.Infrastructure.Data;
using FundManager.BuildingBlocks.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FundManager.FundAdmin.Api.Controllers;

[ApiController]
[Route("api/funds/{fundId:guid}/members")]
[Authorize]
public class MembersController : ControllerBase
{
    private readonly FundService _fundService;
    private readonly FundAdminDbContext _dbContext;

    public MembersController(FundService fundService, FundAdminDbContext dbContext)
    {
        _fundService = fundService;
        _dbContext = dbContext;
    }

    /// <summary>
    /// GET /api/funds/{fundId}/members — List fund members with roles.
    /// </summary>
    [HttpGet]
    [Authorize(Policy = FundAuthorizationPolicies.FundMember)]
    public async Task<IActionResult> ListMembers(
        Guid fundId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken ct = default)
    {
        pageSize = Math.Clamp(pageSize, 1, 100);
        page = Math.Max(1, page);

        var fund = await _dbContext.Funds
            .Include(f => f.RoleAssignments)
            .Include(f => f.MemberPlans)
            .AsNoTracking()
            .FirstOrDefaultAsync(f => f.Id == fundId, ct);

        if (fund is null) return NotFound();

        var members = fund.RoleAssignments
            .Select(ra =>
            {
                var plan = fund.MemberPlans.FirstOrDefault(p => p.UserId == ra.UserId);
                return new MemberSummaryDto
                {
                    UserId = ra.UserId,
                    Name = string.Empty, // Cross-service: would fetch from Identity
                    Role = ra.Role,
                    MonthlyContributionAmount = plan?.MonthlyContributionAmount,
                    JoinDate = plan?.JoinDate,
                    IsActive = plan?.IsActive ?? true
                };
            })
            .OrderBy(m => m.Role)
            .ToList();

        var totalCount = members.Count;
        var pagedItems = members.Skip((page - 1) * pageSize).Take(pageSize).ToList();

        return Ok(new
        {
            items = pagedItems,
            totalCount,
            page,
            pageSize
        });
    }

    /// <summary>
    /// GET /api/funds/{fundId}/members/me/role — Return the current user's role in this fund.
    /// Used by the API Gateway to enrich requests with fund_role header.
    /// </summary>
    [HttpGet("me/role")]
    public async Task<IActionResult> GetMyRole(Guid fundId, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (userId is null) return Unauthorized();

        var assignment = await _dbContext.FundRoleAssignments
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.FundId == fundId && r.UserId == userId.Value, ct);

        if (assignment is null)
            return NotFound(new { message = "Not a member of this fund" });

        return Ok(new { role = assignment.Role });
    }

    /// <summary>
    /// PUT /api/funds/{fundId}/members/{userId}/role — Change a member's role.
    /// </summary>
    [HttpPut("{userId:guid}/role")]
    [Authorize(Policy = FundAuthorizationPolicies.FundAdminOrAbove)]
    public async Task<IActionResult> ChangeRole(Guid fundId, Guid userId, [FromBody] ChangeRoleRequestDto request, CancellationToken ct)
    {
        var currentUserId = GetCurrentUserId();
        if (currentUserId is null) return Unauthorized();

        var result = await _fundService.ChangeRoleAsync(fundId, userId, request.Role, currentUserId.Value, ct);
        if (!result.IsSuccess)
        {
            if (result.ErrorCode == "NOT_FOUND") return NotFound();
            if (result.ErrorCode == "LAST_ADMIN")
                return Conflict(new ProblemDetails { Title = "Cannot demote last Admin", Detail = result.Error, Status = 409 });
            return BadRequest(new ProblemDetails { Title = "Role change failed", Detail = result.Error });
        }

        return Ok();
    }

    /// <summary>
    /// DELETE /api/funds/{fundId}/members/{userId} — Remove a member from the fund.
    /// </summary>
    [HttpDelete("{userId:guid}")]
    [Authorize(Policy = FundAuthorizationPolicies.FundAdminOrAbove)]
    public async Task<IActionResult> RemoveMember(Guid fundId, Guid userId, CancellationToken ct)
    {
        var currentUserId = GetCurrentUserId();
        if (currentUserId is null) return Unauthorized();

        var result = await _fundService.RemoveMemberAsync(fundId, userId, currentUserId.Value, ct);
        if (!result.IsSuccess)
        {
            if (result.ErrorCode == "NOT_FOUND" || result.ErrorCode == "USER_NOT_FOUND") return NotFound();
            if (result.ErrorCode == "LAST_ADMIN")
                return Conflict(new ProblemDetails { Title = "Cannot remove last Admin", Detail = result.Error, Status = 409 });
            return Conflict(new ProblemDetails { Title = "Member has obligations", Detail = result.Error, Status = 409 });
        }

        return NoContent();
    }

    /// <summary>
    /// POST /api/funds/{fundId}/members/{userId}/assign — Assign a role to a user.
    /// </summary>
    [HttpPost("{userId:guid}/assign")]
    [Authorize(Policy = FundAuthorizationPolicies.FundAdminOrAbove)]
    public async Task<IActionResult> AssignRole(Guid fundId, Guid userId, [FromBody] ChangeRoleRequestDto request, CancellationToken ct)
    {
        var currentUserId = GetCurrentUserId();
        if (currentUserId is null) return Unauthorized();

        var result = await _fundService.AssignRoleAsync(fundId, userId, request.Role, currentUserId.Value, ct);
        if (!result.IsSuccess)
        {
            if (result.ErrorCode == "NOT_FOUND") return NotFound();
            return Conflict(new ProblemDetails { Title = "Assignment failed", Detail = result.Error, Status = 409 });
        }

        return CreatedAtAction(nameof(ListMembers), new { fundId }, null);
    }

    private Guid? GetCurrentUserId()
    {
        var sub = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? User.FindFirst("sub")?.Value;
        return sub is not null && Guid.TryParse(sub, out var id) ? id : null;
    }
}

// ── DTOs ────────────────────────────────────────────

public class MemberSummaryDto
{
    public Guid UserId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public decimal? MonthlyContributionAmount { get; set; }
    public DateOnly? JoinDate { get; set; }
    public bool IsActive { get; set; }
}

public class ChangeRoleRequestDto
{
    public string Role { get; set; } = string.Empty;
}
