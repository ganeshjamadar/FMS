using System.Security.Claims;
using FundManager.FundAdmin.Domain.Entities;
using FundManager.FundAdmin.Domain.Services;
using FundManager.BuildingBlocks.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FundManager.FundAdmin.Api.Controllers;

[ApiController]
[Authorize]
public class InvitationsController : ControllerBase
{
    private readonly InvitationService _invitationService;

    public InvitationsController(InvitationService invitationService)
    {
        _invitationService = invitationService;
    }

    /// <summary>
    /// GET /api/funds/{fundId}/invitations — List invitations for a fund.
    /// </summary>
    [HttpGet("api/funds/{fundId:guid}/invitations")]
    [Authorize(Policy = FundAuthorizationPolicies.FundMember)]
    public async Task<IActionResult> ListInvitations(
        Guid fundId,
        [FromQuery] string? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        pageSize = Math.Clamp(pageSize, 1, 100);
        page = Math.Max(1, page);

        InvitationStatus? statusFilter = null;
        if (!string.IsNullOrEmpty(status) && Enum.TryParse<InvitationStatus>(status, ignoreCase: true, out var parsed))
            statusFilter = parsed;

        var (items, totalCount) = await _invitationService.ListAsync(fundId, statusFilter, page, pageSize, ct);

        return Ok(new
        {
            items = items.Select(MapToDto),
            totalCount,
            page,
            pageSize
        });
    }

    /// <summary>
    /// POST /api/funds/{fundId}/invitations — Invite a user to the fund.
    /// </summary>
    [HttpPost("api/funds/{fundId:guid}/invitations")]
    [Authorize(Policy = FundAuthorizationPolicies.FundAdminOrAbove)]
    public async Task<IActionResult> InviteMember(
        Guid fundId,
        [FromBody] InviteMemberRequestDto request,
        CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (userId is null) return Unauthorized();

        var result = await _invitationService.InviteAsync(fundId, request.TargetContact, userId.Value, ct);
        if (!result.IsSuccess)
        {
            if (result.ErrorCode == "NOT_FOUND") return NotFound();
            if (result.ErrorCode == "DUPLICATE") return Conflict(new ProblemDetails { Title = "Duplicate invitation", Detail = result.Error, Status = 409 });
            return UnprocessableEntity(new ProblemDetails { Title = "Invitation failed", Detail = result.Error, Status = 422 });
        }

        return CreatedAtAction(nameof(ListInvitations), new { fundId }, MapToDto(result.Value!));
    }

    /// <summary>
    /// POST /api/invitations/{invitationId}/accept — Accept an invitation.
    /// </summary>
    [HttpPost("api/invitations/{invitationId:guid}/accept")]
    public async Task<IActionResult> AcceptInvitation(
        Guid invitationId,
        [FromBody] AcceptInvitationRequestDto request,
        CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (userId is null) return Unauthorized();

        var result = await _invitationService.AcceptAsync(
            invitationId, userId.Value, request.MonthlyContributionAmount, ct);

        if (!result.IsSuccess)
        {
            if (result.ErrorCode == "NOT_FOUND") return NotFound();
            if (result.ErrorCode == "EXPIRED" || result.ErrorCode == "INVALID_STATE")
                return Conflict(new ProblemDetails { Title = "Cannot accept", Detail = result.Error, Status = 409 });
            if (result.ErrorCode == "BELOW_MINIMUM" || result.ErrorCode == "ALREADY_MEMBER")
                return UnprocessableEntity(new ProblemDetails { Title = "Validation error", Detail = result.Error, Status = 422 });
            return BadRequest(new ProblemDetails { Title = "Error", Detail = result.Error });
        }

        return Ok(new { message = "Invitation accepted. You are now a member." });
    }

    /// <summary>
    /// POST /api/invitations/{invitationId}/decline — Decline an invitation.
    /// </summary>
    [HttpPost("api/invitations/{invitationId:guid}/decline")]
    public async Task<IActionResult> DeclineInvitation(
        Guid invitationId,
        CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (userId is null) return Unauthorized();

        var result = await _invitationService.DeclineAsync(invitationId, userId.Value, ct);
        if (!result.IsSuccess)
        {
            if (result.ErrorCode == "NOT_FOUND") return NotFound();
            return Conflict(new ProblemDetails { Title = "Cannot decline", Detail = result.Error, Status = 409 });
        }

        return Ok(new { message = "Invitation declined." });
    }

    private Guid? GetCurrentUserId()
    {
        var sub = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? User.FindFirst("sub")?.Value;
        return sub is not null && Guid.TryParse(sub, out var id) ? id : null;
    }

    private static InvitationDto MapToDto(Invitation invitation) => new()
    {
        Id = invitation.Id,
        FundId = invitation.FundId,
        TargetContact = invitation.TargetContact,
        InvitedBy = invitation.InvitedBy,
        Status = invitation.Status.ToString(),
        ExpiresAt = invitation.ExpiresAt,
        CreatedAt = invitation.CreatedAt,
        RespondedAt = invitation.RespondedAt
    };
}

// ── DTOs ────────────────────────────────────────────

public class InviteMemberRequestDto
{
    public string TargetContact { get; set; } = string.Empty;
}

public class AcceptInvitationRequestDto
{
    public decimal MonthlyContributionAmount { get; set; }
}

public class InvitationDto
{
    public Guid Id { get; set; }
    public Guid FundId { get; set; }
    public string TargetContact { get; set; } = string.Empty;
    public Guid InvitedBy { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? RespondedAt { get; set; }
}
