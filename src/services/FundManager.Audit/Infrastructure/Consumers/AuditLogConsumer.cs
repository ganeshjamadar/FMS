using FundManager.Audit.Domain.Entities;
using FundManager.Audit.Infrastructure.Data;
using FundManager.Contracts.Events;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace FundManager.Audit.Infrastructure.Consumers;

/// <summary>
/// Consumes AuditLogCreated integration events from all services
/// and persists them as append-only audit log entries.
/// Per research.md Section 2 and contracts/audit-api.yaml.
/// </summary>
public class AuditLogConsumer : IConsumer<AuditLogCreated>
{
    private readonly AuditDbContext _dbContext;
    private readonly ILogger<AuditLogConsumer> _logger;

    public AuditLogConsumer(AuditDbContext dbContext, ILogger<AuditLogConsumer> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<AuditLogCreated> context)
    {
        var msg = context.Message;

        _logger.LogInformation(
            "Processing audit event: {ActionType} on {EntityType}/{EntityId} by {ActorId}",
            msg.ActionType, msg.EntityType, msg.EntityId, msg.ActorId);

        var auditLog = AuditLog.Create(
            actorId: msg.ActorId,
            fundId: msg.FundId,
            actionType: msg.ActionType,
            entityType: msg.EntityType,
            entityId: msg.EntityId,
            beforeState: msg.BeforeState,
            afterState: msg.AfterState,
            ipAddress: msg.IpAddress,
            userAgent: msg.UserAgent,
            correlationId: msg.CorrelationId,
            serviceName: msg.ServiceName);

        _dbContext.AuditLogs.Add(auditLog);
        await _dbContext.SaveChangesAsync(context.CancellationToken);
    }
}
