using FundManager.Audit.Infrastructure.Data;
using FundManager.BuildingBlocks.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FundManager.Audit.Api.Controllers;

[ApiController]
[Route("api/funds/{fundId}/audit")]
[Authorize(Policy = FundAuthorizationPolicies.FundAdminOrAbove)]
public class AuditController : ControllerBase
{
    private readonly AuditDbContext _db;

    public AuditController(AuditDbContext db)
    {
        _db = db;
    }

    // ── GET /api/funds/{fundId}/audit/logs ─────────────

    /// <summary>
    /// Query audit logs for a fund with optional filters.
    /// Always requires date range for partition pruning.
    /// </summary>
    [HttpGet("logs")]
    public async Task<IActionResult> ListAuditLogs(
        [FromRoute] Guid fundId,
        [FromQuery] DateTime fromDate,
        [FromQuery] DateTime toDate,
        [FromQuery] Guid? actorId,
        [FromQuery] string? actionType,
        [FromQuery] string? entityType,
        [FromQuery] Guid? entityId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken ct = default)
    {
        if (pageSize > 200) pageSize = 200;

        var query = _db.AuditLogs
            .AsNoTracking()
            .Where(a => a.FundId == fundId
                        && a.Timestamp >= fromDate.ToUniversalTime()
                        && a.Timestamp < toDate.ToUniversalTime());

        if (actorId.HasValue)
            query = query.Where(a => a.ActorId == actorId.Value);
        if (!string.IsNullOrEmpty(actionType))
            query = query.Where(a => a.ActionType == actionType);
        if (!string.IsNullOrEmpty(entityType))
            query = query.Where(a => a.EntityType == entityType);
        if (entityId.HasValue)
            query = query.Where(a => a.EntityId == entityId.Value);

        var totalCount = await query.CountAsync(ct);

        var items = await query
            .OrderByDescending(a => a.Timestamp)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(a => new AuditLogSummaryDto
            {
                Id = a.Id,
                ActorId = a.ActorId,
                Timestamp = a.Timestamp,
                ActionType = a.ActionType,
                EntityType = a.EntityType,
                EntityId = a.EntityId,
                ServiceName = a.ServiceName,
            })
            .ToListAsync(ct);

        return Ok(new PaginatedAuditLogsDto
        {
            Items = items,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize,
        });
    }

    // ── GET /api/funds/{fundId}/audit/logs/{logId} ─────

    /// <summary>
    /// Get a specific audit log entry with full before/after state.
    /// </summary>
    [HttpGet("logs/{logId:guid}")]
    public async Task<IActionResult> GetAuditLog(
        [FromRoute] Guid fundId,
        [FromRoute] Guid logId,
        CancellationToken ct)
    {
        var log = await _db.AuditLogs
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == logId && a.FundId == fundId, ct);

        if (log is null) return NotFound();

        return Ok(new AuditLogDetailDto
        {
            Id = log.Id,
            ActorId = log.ActorId,
            Timestamp = log.Timestamp,
            ActionType = log.ActionType,
            EntityType = log.EntityType,
            EntityId = log.EntityId,
            ServiceName = log.ServiceName,
            BeforeState = log.BeforeState,
            AfterState = log.AfterState,
            IpAddress = log.IpAddress,
            UserAgent = log.UserAgent,
            CorrelationId = log.CorrelationId,
        });
    }

    // ── GET /api/funds/{fundId}/audit/entity-history ───

    /// <summary>
    /// Get audit trail for a specific entity, sorted chronologically.
    /// </summary>
    [HttpGet("entity-history")]
    public async Task<IActionResult> GetEntityHistory(
        [FromRoute] Guid fundId,
        [FromQuery] string entityType,
        [FromQuery] Guid entityId,
        [FromQuery] DateTime fromDate,
        [FromQuery] DateTime toDate,
        CancellationToken ct)
    {
        var history = await _db.AuditLogs
            .AsNoTracking()
            .Where(a => a.FundId == fundId
                        && a.EntityType == entityType
                        && a.EntityId == entityId
                        && a.Timestamp >= fromDate.ToUniversalTime()
                        && a.Timestamp < toDate.ToUniversalTime())
            .OrderBy(a => a.Timestamp)
            .Select(a => new AuditLogDetailDto
            {
                Id = a.Id,
                ActorId = a.ActorId,
                Timestamp = a.Timestamp,
                ActionType = a.ActionType,
                EntityType = a.EntityType,
                EntityId = a.EntityId,
                ServiceName = a.ServiceName,
                BeforeState = a.BeforeState,
                AfterState = a.AfterState,
                IpAddress = a.IpAddress,
                UserAgent = a.UserAgent,
                CorrelationId = a.CorrelationId,
            })
            .ToListAsync(ct);

        return Ok(history);
    }
}

// ── DTOs ─────────────────────────────────────────────

public class AuditLogSummaryDto
{
    public Guid Id { get; set; }
    public Guid ActorId { get; set; }
    public string? ActorName { get; set; }
    public DateTime Timestamp { get; set; }
    public string ActionType { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty;
    public Guid EntityId { get; set; }
    public string ServiceName { get; set; } = string.Empty;
}

public class AuditLogDetailDto : AuditLogSummaryDto
{
    public string? BeforeState { get; set; }
    public string? AfterState { get; set; }
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public Guid? CorrelationId { get; set; }
}

public class PaginatedAuditLogsDto
{
    public List<AuditLogSummaryDto> Items { get; set; } = [];
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
}
