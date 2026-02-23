using System.Security.Claims;
using FundManager.Loans.Domain.Entities;
using FundManager.Loans.Domain.Services;
using FundManager.BuildingBlocks.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FundManager.Loans.Api.Controllers;

[ApiController]
[Route("api/funds/{fundId:guid}/loans/{loanId:guid}/repayments")]
[Authorize(Policy = FundAuthorizationPolicies.FundMember)]
public class RepaymentsController : ControllerBase
{
    private readonly RepaymentCalculationService _calculationService;
    private readonly RepaymentRecordingService _recordingService;

    public RepaymentsController(
        RepaymentCalculationService calculationService,
        RepaymentRecordingService recordingService)
    {
        _calculationService = calculationService;
        _recordingService = recordingService;
    }

    /// <summary>
    /// GET /api/funds/{fundId}/loans/{loanId}/repayments — List repayment entries for a loan.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> ListRepayments(
        Guid fundId,
        Guid loanId,
        CancellationToken ct)
    {
        var entries = await _calculationService.ListRepaymentsAsync(fundId, loanId, ct);
        return Ok(entries.Select(MapToDto));
    }

    /// <summary>
    /// POST /api/funds/{fundId}/loans/{loanId}/repayments/generate — Generate repayment entry for a month.
    /// Idempotent: re-running returns existing entry.
    /// </summary>
    [HttpPost("generate")]
    [Authorize(Policy = FundAuthorizationPolicies.FundAdminOrAbove)]
    public async Task<IActionResult> GenerateRepayment(
        Guid fundId,
        Guid loanId,
        [FromBody] GenerateRepaymentDto request,
        CancellationToken ct)
    {
        var result = await _calculationService.GenerateRepaymentAsync(
            fundId, loanId, request.MonthYear, ct);

        if (!result.IsSuccess)
        {
            return result.ErrorCode switch
            {
                "NOT_FOUND" => NotFound(new ProblemDetails { Title = "Not found", Detail = result.Error }),
                "INVALID_STATUS" => Conflict(new ProblemDetails { Title = "Invalid loan status", Detail = result.Error, Status = 409 }),
                "VALIDATION" => UnprocessableEntity(new ProblemDetails { Title = "Validation error", Detail = result.Error, Status = 422 }),
                _ => UnprocessableEntity(new ProblemDetails { Title = "Error", Detail = result.Error, Status = 422 })
            };
        }

        return Ok(MapToDto(result.Value!));
    }

    /// <summary>
    /// POST /api/funds/{fundId}/loans/{loanId}/repayments/{repaymentId}/pay — Record a repayment.
    /// Requires Idempotency-Key header. Uses If-Match for optimistic concurrency.
    /// </summary>
    [HttpPost("{repaymentId:guid}/pay")]
    [Authorize(Policy = FundAuthorizationPolicies.FundEditorOrAbove)]
    public async Task<IActionResult> RecordRepayment(
        Guid fundId,
        Guid loanId,
        Guid repaymentId,
        [FromBody] RecordRepaymentRequestDto request,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        [FromHeader(Name = "If-Match")] string? ifMatch,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey))
            return BadRequest(new ProblemDetails { Title = "Missing Idempotency-Key header", Status = 400 });

        var userId = GetCurrentUserId();
        if (userId is null) return Unauthorized();

        var result = await _recordingService.RecordRepaymentAsync(
            fundId, loanId, repaymentId, request.Amount,
            userId.Value, request.Description, ct);

        if (!result.IsSuccess)
        {
            return result.ErrorCode switch
            {
                "NOT_FOUND" => NotFound(new ProblemDetails { Title = "Not found", Detail = result.Error }),
                "INVALID_STATUS" => Conflict(new ProblemDetails { Title = "Invalid status", Detail = result.Error, Status = 409 }),
                "ALREADY_PAID" => Conflict(new ProblemDetails { Title = "Already paid", Detail = result.Error, Status = 409 }),
                "VALIDATION" => UnprocessableEntity(new ProblemDetails { Title = "Validation error", Detail = result.Error, Status = 422 }),
                _ => UnprocessableEntity(new ProblemDetails { Title = "Error", Detail = result.Error, Status = 422 })
            };
        }

        var data = result.Value!;
        return Ok(new RepaymentResultDto
        {
            RepaymentId = data.RepaymentId,
            InterestPaid = data.InterestPaid,
            PrincipalPaid = data.PrincipalPaid,
            ExcessAppliedToPrincipal = data.ExcessAppliedToPrincipal,
            NewOutstandingPrincipal = data.NewOutstandingPrincipal,
            RepaymentStatus = data.RepaymentStatus,
            LoanStatus = data.LoanStatus
        });
    }

    private Guid? GetCurrentUserId()
    {
        var sub = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? User.FindFirst("sub")?.Value;
        return sub is not null && Guid.TryParse(sub, out var id) ? id : null;
    }

    private static RepaymentEntryDto MapToDto(RepaymentEntry entry) => new()
    {
        Id = entry.Id,
        LoanId = entry.LoanId,
        MonthYear = entry.MonthYear,
        InterestDue = entry.InterestDue,
        PrincipalDue = entry.PrincipalDue,
        TotalDue = entry.TotalDue,
        AmountPaid = entry.AmountPaid,
        Status = entry.Status.ToString(),
        DueDate = entry.DueDate,
        PaidDate = entry.PaidDate,
        Version = entry.RowVersion.ToString()
    };
}

// ── DTOs ────────────────────────────────────────────

public class GenerateRepaymentDto
{
    public int MonthYear { get; set; }
}

public class RecordRepaymentRequestDto
{
    public decimal Amount { get; set; }
    public string? Description { get; set; }
}

public class RepaymentEntryDto
{
    public Guid Id { get; set; }
    public Guid LoanId { get; set; }
    public int MonthYear { get; set; }
    public decimal InterestDue { get; set; }
    public decimal PrincipalDue { get; set; }
    public decimal TotalDue { get; set; }
    public decimal AmountPaid { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateOnly DueDate { get; set; }
    public DateTime? PaidDate { get; set; }
    public string Version { get; set; } = string.Empty;
}

public class RepaymentResultDto
{
    public Guid RepaymentId { get; set; }
    public decimal InterestPaid { get; set; }
    public decimal PrincipalPaid { get; set; }
    public decimal ExcessAppliedToPrincipal { get; set; }
    public decimal NewOutstandingPrincipal { get; set; }
    public string RepaymentStatus { get; set; } = string.Empty;
    public string LoanStatus { get; set; } = string.Empty;
}
