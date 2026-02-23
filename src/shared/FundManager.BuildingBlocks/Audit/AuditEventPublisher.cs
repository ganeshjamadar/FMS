using System.Text.Json;
using MassTransit;
using FundManager.Contracts.Events;

namespace FundManager.BuildingBlocks.Audit;

/// <summary>
/// Publishes audit log events via MassTransit.
/// Every state-changing operation in every service should use this.
/// Constitution Principle III: Complete Auditability and Traceability.
/// </summary>
public class AuditEventPublisher
{
    private readonly IPublishEndpoint _publishEndpoint;

    public AuditEventPublisher(IPublishEndpoint publishEndpoint)
    {
        _publishEndpoint = publishEndpoint;
    }

    /// <summary>
    /// Publish an audit event for a state-changing operation.
    /// </summary>
    public async Task PublishAsync(
        Guid? fundId,
        Guid actorId,
        string entityType,
        Guid entityId,
        string actionType,
        object? beforeState,
        object? afterState,
        string serviceName,
        string? ipAddress = null,
        string? userAgent = null,
        Guid? correlationId = null,
        CancellationToken cancellationToken = default)
    {
        var auditEvent = new AuditLogCreated(
            Id: Guid.NewGuid(),
            FundId: fundId,
            ActorId: actorId,
            EntityType: entityType,
            EntityId: entityId,
            ActionType: actionType,
            BeforeState: beforeState is null ? null : JsonSerializer.Serialize(beforeState),
            AfterState: afterState is null ? null : JsonSerializer.Serialize(afterState),
            IpAddress: ipAddress,
            UserAgent: userAgent,
            CorrelationId: correlationId,
            ServiceName: serviceName,
            OccurredAt: DateTime.UtcNow
        );

        await _publishEndpoint.Publish(auditEvent, cancellationToken);
    }
}
