using FundManager.Contributions.Domain.Entities;
using FundManager.Contributions.Infrastructure.Data;
using FundManager.BuildingBlocks.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FundManager.Contributions.Api.Controllers;

[ApiController]
[Route("api/funds/{fundId:guid}/contributions/ledger")]
[Authorize(Policy = FundAuthorizationPolicies.FundMember)]
[Authorize]
public class LedgerController : ControllerBase
{
    private readonly ContributionsDbContext _db;

    public LedgerController(ContributionsDbContext db)
    {
        _db = db;
    }

    /// <summary>
    /// GET /api/funds/{fundId}/contributions/ledger — Fund transaction ledger.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetLedger(
        Guid fundId,
        [FromQuery] string? type,
        [FromQuery] Guid? userId,
        [FromQuery] DateOnly? fromDate,
        [FromQuery] DateOnly? toDate,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken ct = default)
    {
        var query = _db.Transactions
            .AsNoTracking()
            .Where(t => t.FundId == fundId);

        if (!string.IsNullOrEmpty(type) && Enum.TryParse<TransactionType>(type, true, out var txType))
            query = query.Where(t => t.Type == txType);

        if (userId.HasValue)
            query = query.Where(t => t.UserId == userId.Value);

        if (fromDate.HasValue)
            query = query.Where(t => DateOnly.FromDateTime(t.CreatedAt) >= fromDate.Value);

        if (toDate.HasValue)
            query = query.Where(t => DateOnly.FromDateTime(t.CreatedAt) <= toDate.Value);

        var totalCount = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(t => t.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(t => new TransactionEntryDto
            {
                Id = t.Id,
                FundId = t.FundId,
                UserId = t.UserId,
                Type = t.Type.ToString(),
                Amount = t.Amount,
                Description = t.Description,
                ReferenceEntityType = t.ReferenceEntityType,
                ReferenceEntityId = t.ReferenceEntityId,
                RecordedBy = t.RecordedBy,
                CreatedAt = t.CreatedAt
            })
            .ToListAsync(ct);

        return Ok(new { items, totalCount, page, pageSize });
    }
}

public record TransactionEntryDto
{
    public Guid Id { get; init; }
    public Guid FundId { get; init; }
    public Guid UserId { get; init; }
    public string Type { get; init; } = default!;
    public decimal Amount { get; init; }
    public string? Description { get; init; }
    public string? ReferenceEntityType { get; init; }
    public Guid? ReferenceEntityId { get; init; }
    public Guid RecordedBy { get; init; }
    public DateTime CreatedAt { get; init; }
}
