using System.Security.Claims;
using FundManager.Loans.Domain.Entities;
using FundManager.Loans.Domain.Services;
using FundManager.Loans.Infrastructure.Data;
using FundManager.BuildingBlocks.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FundManager.Loans.Api.Controllers;

[ApiController]
[Route("api/funds/{fundId:guid}/loans")]
[Authorize(Policy = FundAuthorizationPolicies.FundMember)]
public class LoansController : ControllerBase
{
    private readonly LoanRequestService _loanService;
    private readonly LoansDbContext _db;

    public LoansController(LoanRequestService loanService, LoansDbContext db)
    {
        _loanService = loanService;
        _db = db;
    }

    /// <summary>
    /// GET /api/funds/{fundId}/loans — List loans for the fund.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> ListLoans(
        Guid fundId,
        [FromQuery] string? status,
        [FromQuery] Guid? borrowerId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        pageSize = Math.Clamp(pageSize, 1, 100);
        page = Math.Max(1, page);

        var query = _db.Loans.AsNoTracking()
            .Where(l => l.FundId == fundId);

        if (!string.IsNullOrEmpty(status) && Enum.TryParse<LoanStatus>(status, ignoreCase: true, out var s))
            query = query.Where(l => l.Status == s);

        if (borrowerId.HasValue)
            query = query.Where(l => l.BorrowerId == borrowerId.Value);

        var totalCount = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(l => l.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return Ok(new
        {
            items = items.Select(MapToDto),
            totalCount,
            page,
            pageSize
        });
    }

    /// <summary>
    /// POST /api/funds/{fundId}/loans — Submit a loan request.
    /// </summary>
    [HttpPost]
    [Authorize(Policy = FundAuthorizationPolicies.FundEditorOrAbove)]
    public async Task<IActionResult> RequestLoan(
        Guid fundId,
        [FromBody] LoanRequestDto request,
        CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (userId is null) return Unauthorized();

        var result = await _loanService.RequestLoanAsync(
            fundId, userId.Value, request.PrincipalAmount,
            request.RequestedStartMonth, request.Purpose, ct);

        if (!result.IsSuccess)
        {
            return result.ErrorCode switch
            {
                "NOT_FOUND" => NotFound(new ProblemDetails { Title = "Fund not found", Detail = result.Error }),
                "MAX_LOAN_EXCEEDED" => Conflict(new ProblemDetails { Title = "Loan cap exceeded", Detail = result.Error, Status = 409 }),
                "MAX_CONCURRENT_LOANS" => Conflict(new ProblemDetails { Title = "Max concurrent loans", Detail = result.Error, Status = 409 }),
                _ => UnprocessableEntity(new ProblemDetails { Title = "Validation error", Detail = result.Error, Status = 422 })
            };
        }

        return CreatedAtAction(nameof(GetLoan), new { fundId, loanId = result.Value!.Id }, MapToDto(result.Value));
    }

    /// <summary>
    /// GET /api/funds/{fundId}/loans/{loanId} — Get loan details.
    /// </summary>
    [HttpGet("{loanId:guid}")]
    public async Task<IActionResult> GetLoan(Guid fundId, Guid loanId, CancellationToken ct)
    {
        var loan = await _loanService.GetLoanAsync(fundId, loanId, ct);
        if (loan is null) return NotFound();

        return Ok(MapToDto(loan));
    }

    /// <summary>
    /// POST /api/funds/{fundId}/loans/{loanId}/approve — Approve a loan (Fund Admin).
    /// </summary>
    [HttpPost("{loanId:guid}/approve")]
    [Authorize(Policy = FundAuthorizationPolicies.FundAdminOrAbove)]
    public async Task<IActionResult> ApproveLoan(
        Guid fundId,
        Guid loanId,
        [FromBody] ApproveLoanRequestDto request,
        CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (userId is null) return Unauthorized();

        var result = await _loanService.ApproveLoanAsync(
            fundId, loanId, userId.Value, request.ScheduledInstallment, ct);

        if (!result.IsSuccess)
        {
            return result.ErrorCode switch
            {
                "NOT_FOUND" => NotFound(new ProblemDetails { Title = "Not found", Detail = result.Error }),
                "INVALID_STATUS" => Conflict(new ProblemDetails { Title = "Invalid status", Detail = result.Error, Status = 409 }),
                _ => UnprocessableEntity(new ProblemDetails { Title = "Validation error", Detail = result.Error, Status = 422 })
            };
        }

        return Ok(MapToDto(result.Value!));
    }

    /// <summary>
    /// POST /api/funds/{fundId}/loans/{loanId}/reject — Reject a loan (Fund Admin).
    /// </summary>
    [HttpPost("{loanId:guid}/reject")]
    [Authorize(Policy = FundAuthorizationPolicies.FundAdminOrAbove)]
    public async Task<IActionResult> RejectLoan(
        Guid fundId,
        Guid loanId,
        [FromBody] RejectLoanRequestDto request,
        CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (userId is null) return Unauthorized();

        var result = await _loanService.RejectLoanAsync(
            fundId, loanId, userId.Value, request.Reason, ct);

        if (!result.IsSuccess)
        {
            return result.ErrorCode switch
            {
                "NOT_FOUND" => NotFound(new ProblemDetails { Title = "Not found", Detail = result.Error }),
                "INVALID_STATUS" => Conflict(new ProblemDetails { Title = "Invalid status", Detail = result.Error, Status = 409 }),
                _ => UnprocessableEntity(new ProblemDetails { Title = "Error", Detail = result.Error })
            };
        }

        return Ok(MapToDto(result.Value!));
    }

    private Guid? GetCurrentUserId()
    {
        var sub = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? User.FindFirst("sub")?.Value;
        return sub is not null && Guid.TryParse(sub, out var id) ? id : null;
    }

    private static LoanDto MapToDto(Loan loan) => new()
    {
        Id = loan.Id,
        FundId = loan.FundId,
        BorrowerId = loan.BorrowerId,
        PrincipalAmount = loan.PrincipalAmount,
        OutstandingPrincipal = loan.OutstandingPrincipal,
        MonthlyInterestRate = loan.MonthlyInterestRate,
        ScheduledInstallment = loan.ScheduledInstallment,
        MinimumPrincipal = loan.MinimumPrincipal,
        RequestedStartMonth = loan.RequestedStartMonth,
        Purpose = loan.Purpose,
        Status = loan.Status.ToString(),
        ApprovedBy = loan.ApprovedBy,
        RejectionReason = loan.RejectionReason,
        ApprovalDate = loan.ApprovalDate,
        DisbursementDate = loan.DisbursementDate,
        ClosedDate = loan.ClosedDate,
        CreatedAt = loan.CreatedAt
    };
}

// ── DTOs ────────────────────────────────────────────

public class LoanRequestDto
{
    public decimal PrincipalAmount { get; set; }
    public int RequestedStartMonth { get; set; }
    public string? Purpose { get; set; }
}

public class ApproveLoanRequestDto
{
    public decimal ScheduledInstallment { get; set; }
}

public class RejectLoanRequestDto
{
    public string Reason { get; set; } = string.Empty;
}

public class LoanDto
{
    public Guid Id { get; set; }
    public Guid FundId { get; set; }
    public Guid BorrowerId { get; set; }
    public decimal PrincipalAmount { get; set; }
    public decimal OutstandingPrincipal { get; set; }
    public decimal MonthlyInterestRate { get; set; }
    public decimal ScheduledInstallment { get; set; }
    public decimal MinimumPrincipal { get; set; }
    public int RequestedStartMonth { get; set; }
    public string? Purpose { get; set; }
    public string Status { get; set; } = string.Empty;
    public Guid? ApprovedBy { get; set; }
    public string? RejectionReason { get; set; }
    public DateTime? ApprovalDate { get; set; }
    public DateTime? DisbursementDate { get; set; }
    public DateTime? ClosedDate { get; set; }
    public DateTime CreatedAt { get; set; }
}
