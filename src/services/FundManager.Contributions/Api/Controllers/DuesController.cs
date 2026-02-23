using FundManager.Contributions.Domain.Entities;
using FundManager.Contributions.Domain.Services;
using FundManager.Contributions.Infrastructure.Data;
using FundManager.BuildingBlocks.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FundManager.Contributions.Api.Controllers;

[ApiController]
[Route("api/funds/{fundId:guid}/contributions/dues")]
[Authorize(Policy = FundAuthorizationPolicies.FundMember)]
public class DuesController : ControllerBase
{
    private readonly ContributionsDbContext _db;
    private readonly ContributionCycleService _cycleService;

    public DuesController(ContributionsDbContext db, ContributionCycleService cycleService)
    {
        _db = db;
        _cycleService = cycleService;
    }

    /// <summary>
    /// GET /api/funds/{fundId}/contributions/dues — List contribution dues.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> ListDues(
        Guid fundId,
        [FromQuery] int? monthYear,
        [FromQuery] Guid? userId,
        [FromQuery] string? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken ct = default)
    {
        var query = _db.ContributionDues
            .AsNoTracking()
            .Where(d => d.FundId == fundId);

        if (monthYear.HasValue)
            query = query.Where(d => d.MonthYear == monthYear.Value);

        if (userId.HasValue)
            query = query.Where(d => d.UserId == userId.Value);

        if (!string.IsNullOrEmpty(status) && Enum.TryParse<ContributionDueStatus>(status, true, out var s))
            query = query.Where(d => d.Status == s);

        var totalCount = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(d => d.MonthYear)
            .ThenBy(d => d.UserId)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(d => new ContributionDueDto
            {
                Id = d.Id,
                FundId = d.FundId,
                UserId = d.UserId,
                MonthYear = d.MonthYear,
                AmountDue = d.AmountDue,
                AmountPaid = d.AmountPaid,
                RemainingBalance = d.RemainingBalance,
                Status = d.Status.ToString(),
                DueDate = d.DueDate,
                PaidDate = d.PaidDate,
                Version = d.RowVersion.ToString()
            })
            .ToListAsync(ct);

        return Ok(new { items, totalCount, page, pageSize });
    }

    /// <summary>
    /// GET /api/funds/{fundId}/contributions/dues/{dueId} — Get a specific due.
    /// </summary>
    [HttpGet("{dueId:guid}")]
    public async Task<IActionResult> GetDue(Guid fundId, Guid dueId, CancellationToken ct)
    {
        var due = await _db.ContributionDues
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == dueId && d.FundId == fundId, ct);

        if (due is null)
            return NotFound();

        return Ok(new ContributionDueDto
        {
            Id = due.Id,
            FundId = due.FundId,
            UserId = due.UserId,
            MonthYear = due.MonthYear,
            AmountDue = due.AmountDue,
            AmountPaid = due.AmountPaid,
            RemainingBalance = due.RemainingBalance,
            Status = due.Status.ToString(),
            DueDate = due.DueDate,
            PaidDate = due.PaidDate,
            Version = due.RowVersion.ToString()
        });
    }

    /// <summary>
    /// POST /api/funds/{fundId}/contributions/dues/generate — Trigger monthly due generation.
    /// </summary>
    [HttpPost("generate")]
    [Authorize(Policy = FundAuthorizationPolicies.FundAdminOrAbove)]
    public async Task<IActionResult> GenerateDues(
        Guid fundId,
        [FromBody] GenerateDuesRequestDto request,
        CancellationToken ct)
    {
        var actorId = GetUserId();
        var result = await _cycleService.GenerateDuesAsync(fundId, request.MonthYear, actorId, ct);

        if (!result.IsSuccess)
            return result.ErrorCode switch
            {
                "NO_MEMBERS" => Conflict(new { error = result.Error }),
                _ => BadRequest(new { error = result.Error })
            };

        var data = result.Value;
        return Ok(new { generated = data.Generated, skipped = data.Skipped });
    }

    /// <summary>
    /// GET /api/funds/{fundId}/contributions/summary — Monthly contribution summary.
    /// </summary>
    [HttpGet("~/api/funds/{fundId:guid}/contributions/summary")]
    public async Task<IActionResult> GetSummary(
        Guid fundId,
        [FromQuery] int monthYear,
        CancellationToken ct)
    {
        var dues = await _db.ContributionDues
            .AsNoTracking()
            .Where(d => d.FundId == fundId && d.MonthYear == monthYear)
            .ToListAsync(ct);

        return Ok(new
        {
            fundId,
            monthYear,
            totalDue = dues.Sum(d => d.AmountDue),
            totalCollected = dues.Sum(d => d.AmountPaid),
            totalOutstanding = dues.Sum(d => d.RemainingBalance),
            paidCount = dues.Count(d => d.Status == ContributionDueStatus.Paid),
            partialCount = dues.Count(d => d.Status == ContributionDueStatus.Partial),
            pendingCount = dues.Count(d => d.Status == ContributionDueStatus.Pending),
            lateCount = dues.Count(d => d.Status == ContributionDueStatus.Late),
            missedCount = dues.Count(d => d.Status == ContributionDueStatus.Missed)
        });
    }

    private Guid GetUserId()
    {
        var sub = User.FindFirst("sub")?.Value;
        return sub is not null ? Guid.Parse(sub) : Guid.Empty;
    }
}

// DTOs

public record GenerateDuesRequestDto
{
    public int MonthYear { get; init; }
}

public record ContributionDueDto
{
    public Guid Id { get; init; }
    public Guid FundId { get; init; }
    public Guid UserId { get; init; }
    public int MonthYear { get; init; }
    public decimal AmountDue { get; init; }
    public decimal AmountPaid { get; init; }
    public decimal RemainingBalance { get; init; }
    public string Status { get; init; } = default!;
    public DateOnly DueDate { get; init; }
    public DateTime? PaidDate { get; init; }
    public string Version { get; init; } = default!;
}
