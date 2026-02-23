using FundManager.BuildingBlocks.Domain;

namespace FundManager.Audit.Domain.Entities;

/// <summary>
/// Append-only audit log entry.
/// Per data-model.md AuditLog entity and research-database.md Section 13.
/// </summary>
public class AuditLog : Entity
{
    public Guid ActorId { get; private set; }
    public Guid? FundId { get; private set; }
    public DateTime Timestamp { get; private set; }
    public string ActionType { get; private set; } = string.Empty;
    public string EntityType { get; private set; } = string.Empty;
    public Guid EntityId { get; private set; }
    public string? BeforeState { get; private set; } // JSON
    public string? AfterState { get; private set; }  // JSON
    public string? IpAddress { get; private set; }
    public string? UserAgent { get; private set; }
    public Guid? CorrelationId { get; private set; }
    public string ServiceName { get; private set; } = string.Empty;

    private AuditLog() { } // EF Core

    public static AuditLog Create(
        Guid actorId,
        Guid? fundId,
        string actionType,
        string entityType,
        Guid entityId,
        string? beforeState,
        string? afterState,
        string? ipAddress,
        string? userAgent,
        Guid? correlationId,
        string serviceName)
    {
        return new AuditLog
        {
            Id = Guid.NewGuid(),
            ActorId = actorId,
            FundId = fundId,
            Timestamp = DateTime.UtcNow,
            ActionType = actionType,
            EntityType = entityType,
            EntityId = entityId,
            BeforeState = beforeState,
            AfterState = afterState,
            IpAddress = ipAddress,
            UserAgent = userAgent,
            CorrelationId = correlationId,
            ServiceName = serviceName,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
    }
}
