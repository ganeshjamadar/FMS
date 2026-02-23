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
[Route("api/funds")]
[Authorize]
public class FundsController : ControllerBase
{
    private readonly FundService _fundService;
    private readonly FundAdminDbContext _dbContext;

    public FundsController(FundService fundService, FundAdminDbContext dbContext)
    {
        _fundService = fundService;
        _dbContext = dbContext;
    }

    /// <summary>
    /// GET /api/funds — List funds (Platform Admin sees all; users see their funds).
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> ListFunds(
        [FromQuery] string? state,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        pageSize = Math.Clamp(pageSize, 1, 100);
        page = Math.Max(1, page);

        var query = _dbContext.Funds.AsNoTracking().AsQueryable();

        // Filter by state if specified
        if (!string.IsNullOrEmpty(state) && Enum.TryParse<FundState>(state, ignoreCase: true, out var fundState))
        {
            query = query.Where(f => f.State == fundState);
        }

        // TODO: Scope to user's funds unless Platform Admin
        // For now, return all funds the user has access to

        var totalCount = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(f => f.CreatedAt)
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
    /// POST /api/funds — Create a new fund (Platform Admin only).
    /// </summary>
    [HttpPost]
    [Authorize(Policy = FundAuthorizationPolicies.PlatformAdmin)]
    public async Task<IActionResult> CreateFund([FromBody] CreateFundRequestDto request, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (userId is null) return Unauthorized();

        var result = await _fundService.CreateFundAsync(
            name: request.Name,
            monthlyInterestRate: request.MonthlyInterestRate,
            minimumMonthlyContribution: request.MinimumMonthlyContribution,
            minimumPrincipalPerRepayment: request.MinimumPrincipalPerRepayment,
            creatorId: userId.Value,
            description: request.Description,
            currency: request.Currency ?? "INR",
            loanApprovalPolicy: request.LoanApprovalPolicy ?? "AdminOnly",
            maxLoanPerMember: request.MaxLoanPerMember,
            maxConcurrentLoans: request.MaxConcurrentLoans,
            dissolutionPolicy: request.DissolutionPolicy,
            overduePenaltyType: request.OverduePenaltyType ?? "None",
            overduePenaltyValue: request.OverduePenaltyValue,
            contributionDayOfMonth: request.ContributionDayOfMonth,
            gracePeriodDays: request.GracePeriodDays,
            ct: ct);

        if (!result.IsSuccess)
        {
            return UnprocessableEntity(new ProblemDetails
            {
                Title = "Validation error",
                Detail = result.Error,
                Status = 422
            });
        }

        return CreatedAtAction(nameof(GetFund), new { fundId = result.Value!.Id }, MapToDto(result.Value));
    }

    /// <summary>
    /// GET /api/funds/{fundId} — Get fund details.
    /// </summary>
    [HttpGet("{fundId:guid}")]
    [Authorize(Policy = FundAuthorizationPolicies.FundMember)]
    public async Task<IActionResult> GetFund(Guid fundId, CancellationToken ct)
    {
        var fund = await _fundService.GetFundAsync(fundId, ct);
        if (fund is null) return NotFound();

        return Ok(MapToDto(fund));
    }

    /// <summary>
    /// PATCH /api/funds/{fundId} — Update fund fields.
    /// Description is always updatable. All other config fields are only updatable while fund is in Draft state.
    /// </summary>
    [HttpPatch("{fundId:guid}")]
    [Authorize(Policy = FundAuthorizationPolicies.FundAdminOrAbove)]
    public async Task<IActionResult> UpdateFund(Guid fundId, [FromBody] UpdateFundRequestDto request, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (userId is null) return Unauthorized();

        var result = await _fundService.UpdateFundAsync(fundId, request, userId.Value, ct);
        if (!result.IsSuccess)
        {
            if (result.ErrorCode == "NOT_FOUND") return NotFound();
            if (result.ErrorCode == "INVALID_STATE") return Conflict(new ProblemDetails { Title = "Update not allowed", Detail = result.Error, Status = 409 });
            return UnprocessableEntity(new ProblemDetails { Title = "Update failed", Detail = result.Error });
        }

        return Ok(MapToDto(result.Value!));
    }

    /// <summary>
    /// POST /api/funds/{fundId}/activate — Transition fund Draft → Active.
    /// </summary>
    [HttpPost("{fundId:guid}/activate")]
    [Authorize(Policy = FundAuthorizationPolicies.FundAdminOrAbove)]
    public async Task<IActionResult> ActivateFund(Guid fundId, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (userId is null) return Unauthorized();

        var result = await _fundService.ActivateFundAsync(fundId, userId.Value, ct);
        if (!result.IsSuccess)
        {
            if (result.ErrorCode == "NOT_FOUND") return NotFound();
            return Conflict(new ProblemDetails { Title = "Activation failed", Detail = result.Error, Status = 409 });
        }

        return Ok(MapToDto(result.Value!));
    }

    /// <summary>
    /// GET /api/funds/{fundId}/dashboard — Fund dashboard summary.
    /// </summary>
    [HttpGet("{fundId:guid}/dashboard")]
    [Authorize(Policy = FundAuthorizationPolicies.FundMember)]
    public async Task<IActionResult> GetDashboard(Guid fundId, CancellationToken ct)
    {
        var fund = await _dbContext.Funds
            .Include(f => f.RoleAssignments)
            .Include(f => f.MemberPlans)
            .AsNoTracking()
            .FirstOrDefaultAsync(f => f.Id == fundId, ct);

        if (fund is null) return NotFound();

        return Ok(new FundDashboardDto
        {
            FundId = fund.Id,
            FundName = fund.Name,
            State = fund.State.ToString(),
            TotalBalance = 0, // Computed via contributions service cross-query
            MemberCount = fund.MemberPlans.Count(p => p.IsActive),
            ActiveLoansCount = 0, // Cross-service
            PendingApprovalsCount = 0, // Cross-service
            OverdueContributionsCount = 0, // Cross-service
            OverdueRepaymentsCount = 0, // Cross-service
            ThisMonthContributionsCollected = 0, // Cross-service
            ThisMonthContributionsDue = 0 // Cross-service
        });
    }

    private Guid? GetCurrentUserId()
    {
        var sub = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? User.FindFirst("sub")?.Value;
        return sub is not null && Guid.TryParse(sub, out var id) ? id : null;
    }

    private static FundDto MapToDto(Fund fund) => new()
    {
        Id = fund.Id,
        Name = fund.Name,
        Description = fund.Description,
        Currency = fund.Currency,
        MonthlyInterestRate = fund.MonthlyInterestRate,
        MinimumMonthlyContribution = fund.MinimumMonthlyContribution,
        MinimumPrincipalPerRepayment = fund.MinimumPrincipalPerRepayment,
        LoanApprovalPolicy = fund.LoanApprovalPolicy,
        MaxLoanPerMember = fund.MaxLoanPerMember,
        MaxConcurrentLoans = fund.MaxConcurrentLoans,
        DissolutionPolicy = fund.DissolutionPolicy,
        OverduePenaltyType = fund.OverduePenaltyType,
        OverduePenaltyValue = fund.OverduePenaltyValue,
        ContributionDayOfMonth = fund.ContributionDayOfMonth,
        GracePeriodDays = fund.GracePeriodDays,
        State = fund.State.ToString(),
        CreatedAt = fund.CreatedAt,
        UpdatedAt = fund.UpdatedAt
    };
}

// ── DTOs ────────────────────────────────────────────

public class CreateFundRequestDto
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal MonthlyInterestRate { get; set; }
    public decimal MinimumMonthlyContribution { get; set; }
    public decimal MinimumPrincipalPerRepayment { get; set; } = 1000.00m;
    public string? Currency { get; set; }
    public string? LoanApprovalPolicy { get; set; }
    public decimal? MaxLoanPerMember { get; set; }
    public int? MaxConcurrentLoans { get; set; }
    public string? DissolutionPolicy { get; set; }
    public string? OverduePenaltyType { get; set; }
    public decimal OverduePenaltyValue { get; set; }
    public int ContributionDayOfMonth { get; set; } = 1;
    public int GracePeriodDays { get; set; } = 5;
}

public class UpdateFundRequestDto
{
    public string? Description { get; set; }
    // Config fields — only applied when fund is in Draft state
    public string? Name { get; set; }
    public decimal? MonthlyInterestRate { get; set; }
    public decimal? MinimumMonthlyContribution { get; set; }
    public decimal? MinimumPrincipalPerRepayment { get; set; }
    public string? Currency { get; set; }
    public string? LoanApprovalPolicy { get; set; }
    public decimal? MaxLoanPerMember { get; set; }
    public bool ClearMaxLoanPerMember { get; set; }
    public int? MaxConcurrentLoans { get; set; }
    public bool ClearMaxConcurrentLoans { get; set; }
    public string? DissolutionPolicy { get; set; }
    public string? OverduePenaltyType { get; set; }
    public decimal? OverduePenaltyValue { get; set; }
    public int? ContributionDayOfMonth { get; set; }
    public int? GracePeriodDays { get; set; }
}

public class FundDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Currency { get; set; } = string.Empty;
    public decimal MonthlyInterestRate { get; set; }
    public decimal MinimumMonthlyContribution { get; set; }
    public decimal MinimumPrincipalPerRepayment { get; set; }
    public string LoanApprovalPolicy { get; set; } = string.Empty;
    public decimal? MaxLoanPerMember { get; set; }
    public int? MaxConcurrentLoans { get; set; }
    public string? DissolutionPolicy { get; set; }
    public string OverduePenaltyType { get; set; } = string.Empty;
    public decimal OverduePenaltyValue { get; set; }
    public int ContributionDayOfMonth { get; set; }
    public int GracePeriodDays { get; set; }
    public string State { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class FundDashboardDto
{
    public Guid FundId { get; set; }
    public string FundName { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public decimal TotalBalance { get; set; }
    public int MemberCount { get; set; }
    public int ActiveLoansCount { get; set; }
    public int PendingApprovalsCount { get; set; }
    public int OverdueContributionsCount { get; set; }
    public int OverdueRepaymentsCount { get; set; }
    public decimal ThisMonthContributionsCollected { get; set; }
    public decimal ThisMonthContributionsDue { get; set; }
}
