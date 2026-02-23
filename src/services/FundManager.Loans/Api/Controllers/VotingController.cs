using System.Security.Claims;
using FundManager.Loans.Domain.Entities;
using FundManager.Loans.Domain.Services;
using FundManager.BuildingBlocks.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FundManager.Loans.Api.Controllers;

[ApiController]
[Route("api/funds/{fundId:guid}/loans/{loanId:guid}/voting")]
[Authorize(Policy = FundAuthorizationPolicies.FundMember)]
public class VotingController : ControllerBase
{
    private readonly VotingService _votingService;

    public VotingController(VotingService votingService)
    {
        _votingService = votingService;
    }

    /// <summary>
    /// POST /api/funds/{fundId}/loans/{loanId}/voting — Start a voting session (Fund Admin).
    /// </summary>
    [HttpPost]
    [Authorize(Policy = FundAuthorizationPolicies.FundAdminOrAbove)]
    public async Task<IActionResult> StartVoting(
        Guid fundId,
        Guid loanId,
        [FromBody] StartVotingRequestDto request,
        CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (userId is null) return Unauthorized();

        var result = await _votingService.StartVotingAsync(
            fundId, loanId, userId.Value,
            request.VotingWindowHours,
            request.ThresholdType,
            request.ThresholdValue, ct);

        if (!result.IsSuccess)
        {
            return result.ErrorCode switch
            {
                "NOT_FOUND" => NotFound(new ProblemDetails { Title = "Not found", Detail = result.Error }),
                "INVALID_STATUS" => Conflict(new ProblemDetails { Title = "Invalid status", Detail = result.Error, Status = 409 }),
                "ALREADY_EXISTS" => Conflict(new ProblemDetails { Title = "Already exists", Detail = result.Error, Status = 409 }),
                _ => UnprocessableEntity(new ProblemDetails { Title = "Error", Detail = result.Error, Status = 422 })
            };
        }

        return CreatedAtAction(nameof(GetVotingSession), new { fundId, loanId }, MapSessionToDto(result.Value!));
    }

    /// <summary>
    /// GET /api/funds/{fundId}/loans/{loanId}/voting — Get voting session details and tally.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetVotingSession(
        Guid fundId,
        Guid loanId,
        CancellationToken ct)
    {
        var session = await _votingService.GetVotingSessionAsync(fundId, loanId, ct);
        if (session is null) return NotFound();

        var approveCount = session.Votes.Count(v => v.Decision == "Approve");
        var rejectCount = session.Votes.Count(v => v.Decision == "Reject");

        return Ok(new VotingSessionDetailDto
        {
            Id = session.Id,
            LoanId = session.LoanId,
            VotingWindowStart = session.VotingWindowStart,
            VotingWindowEnd = session.VotingWindowEnd,
            ThresholdType = session.ThresholdType,
            ThresholdValue = session.ThresholdValue,
            Result = session.Result.ToString(),
            OverrideUsed = session.OverrideUsed,
            FinalisedBy = session.FinalisedBy,
            FinalisedDate = session.FinalisedDate,
            ApproveCount = approveCount,
            RejectCount = rejectCount,
            TotalEligible = approveCount + rejectCount, // Simplification: total who voted
            Votes = session.Votes.Select(v => new VoteSummaryDto
            {
                VoterId = v.VoterId,
                Decision = v.Decision,
                CastAt = v.CastAt
            }).OrderBy(v => v.CastAt).ToList()
        });
    }

    /// <summary>
    /// POST /api/funds/{fundId}/loans/{loanId}/voting/vote — Cast a vote (Editor).
    /// </summary>
    [HttpPost("vote")]
    [Authorize(Policy = FundAuthorizationPolicies.FundEditorOrAbove)]
    public async Task<IActionResult> CastVote(
        Guid fundId,
        Guid loanId,
        [FromBody] CastVoteRequestDto request,
        CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (userId is null) return Unauthorized();

        var result = await _votingService.CastVoteAsync(
            fundId, loanId, userId.Value, request.Decision, ct);

        if (!result.IsSuccess)
        {
            return result.ErrorCode switch
            {
                "NOT_FOUND" => NotFound(new ProblemDetails { Title = "Not found", Detail = result.Error }),
                "ALREADY_FINALISED" => Conflict(new ProblemDetails { Title = "Voting closed", Detail = result.Error, Status = 409 }),
                "WINDOW_CLOSED" => Conflict(new ProblemDetails { Title = "Window closed", Detail = result.Error, Status = 409 }),
                "ALREADY_VOTED" => Conflict(new ProblemDetails { Title = "Already voted", Detail = result.Error, Status = 409 }),
                "VALIDATION" => UnprocessableEntity(new ProblemDetails { Title = "Validation error", Detail = result.Error, Status = 422 }),
                _ => UnprocessableEntity(new ProblemDetails { Title = "Error", Detail = result.Error, Status = 422 })
            };
        }

        return Ok(new { message = "Vote recorded." });
    }

    /// <summary>
    /// POST /api/funds/{fundId}/loans/{loanId}/voting/finalise — Finalise voting (Fund Admin).
    /// </summary>
    [HttpPost("finalise")]
    [Authorize(Policy = FundAuthorizationPolicies.FundAdminOrAbove)]
    public async Task<IActionResult> FinaliseVoting(
        Guid fundId,
        Guid loanId,
        [FromBody] FinaliseVotingRequestDto request,
        CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (userId is null) return Unauthorized();

        var result = await _votingService.FinaliseVotingAsync(
            fundId, loanId, userId.Value, request.Decision, ct);

        if (!result.IsSuccess)
        {
            return result.ErrorCode switch
            {
                "NOT_FOUND" => NotFound(new ProblemDetails { Title = "Not found", Detail = result.Error }),
                "ALREADY_FINALISED" => Conflict(new ProblemDetails { Title = "Already finalised", Detail = result.Error, Status = 409 }),
                "VALIDATION" => UnprocessableEntity(new ProblemDetails { Title = "Validation error", Detail = result.Error, Status = 422 }),
                _ => UnprocessableEntity(new ProblemDetails { Title = "Error", Detail = result.Error, Status = 422 })
            };
        }

        return Ok(MapSessionToDto(result.Value!));
    }

    private Guid? GetCurrentUserId()
    {
        var sub = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? User.FindFirst("sub")?.Value;
        return sub is not null && Guid.TryParse(sub, out var id) ? id : null;
    }

    private static VotingSessionDto MapSessionToDto(VotingSession session) => new()
    {
        Id = session.Id,
        LoanId = session.LoanId,
        VotingWindowStart = session.VotingWindowStart,
        VotingWindowEnd = session.VotingWindowEnd,
        ThresholdType = session.ThresholdType,
        ThresholdValue = session.ThresholdValue,
        Result = session.Result.ToString(),
        OverrideUsed = session.OverrideUsed,
        FinalisedBy = session.FinalisedBy,
        FinalisedDate = session.FinalisedDate
    };
}

// ── DTOs ────────────────────────────────────────────

public class StartVotingRequestDto
{
    public int VotingWindowHours { get; set; } = 48;
    public string ThresholdType { get; set; } = "Majority";
    public decimal ThresholdValue { get; set; } = 50.00m;
}

public class CastVoteRequestDto
{
    public string Decision { get; set; } = string.Empty;
}

public class FinaliseVotingRequestDto
{
    public string Decision { get; set; } = string.Empty;
}

public class VotingSessionDto
{
    public Guid Id { get; set; }
    public Guid LoanId { get; set; }
    public DateTime VotingWindowStart { get; set; }
    public DateTime VotingWindowEnd { get; set; }
    public string ThresholdType { get; set; } = string.Empty;
    public decimal ThresholdValue { get; set; }
    public string Result { get; set; } = string.Empty;
    public bool OverrideUsed { get; set; }
    public Guid? FinalisedBy { get; set; }
    public DateTime? FinalisedDate { get; set; }
}

public class VotingSessionDetailDto : VotingSessionDto
{
    public int ApproveCount { get; set; }
    public int RejectCount { get; set; }
    public int TotalEligible { get; set; }
    public List<VoteSummaryDto> Votes { get; set; } = new();
}

public class VoteSummaryDto
{
    public Guid VoterId { get; set; }
    public string Decision { get; set; } = string.Empty;
    public DateTime CastAt { get; set; }
}
