using FundManager.BuildingBlocks.Audit;
using FundManager.BuildingBlocks.Domain;
using FundManager.Contracts.Events;
using FundManager.Contributions.Domain.Entities;
using FundManager.Contributions.Infrastructure.Data;
using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace FundManager.Contributions.Domain.Services;

/// <summary>
/// Records contribution payments with idempotency and optimistic concurrency.
/// FR-035: Admin records observed payments. FR-035a: Optimistic concurrency via If-Match.
/// FR-037: Partial payments allowed.
/// </summary>
public class PaymentService
{
    private readonly ContributionsDbContext _db;
    private readonly IPublishEndpoint _publish;
    private readonly AuditEventPublisher _audit;

    public PaymentService(
        ContributionsDbContext db,
        IPublishEndpoint publish,
        AuditEventPublisher audit)
    {
        _db = db;
        _publish = publish;
        _audit = audit;
    }

    public record PaymentResult(
        Guid TransactionId,
        Guid DueId,
        decimal AmountPaid,
        decimal RemainingBalance,
        string NewStatus);

    /// <summary>
    /// Record a payment against a contribution due.
    /// FR-114: Idempotency-Key header for deduplication.
    /// FR-035a: If-Match for optimistic concurrency.
    /// </summary>
    public async Task<Result<PaymentResult>> RecordPaymentAsync(
        Guid fundId,
        Guid dueId,
        decimal amount,
        string idempotencyKey,
        uint expectedVersion,
        Guid recordedBy,
        string? description = null,
        CancellationToken ct = default)
    {
        // Check idempotency — return cached result if already processed
        var existingTx = await _db.Transactions
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.FundId == fundId && t.IdempotencyKey == idempotencyKey, ct);

        if (existingTx is not null)
        {
            // Already processed — return idempotent result
            var existingDue = await _db.ContributionDues.AsNoTracking()
                .FirstOrDefaultAsync(d => d.Id == dueId, ct);

            return Result<PaymentResult>.Success(new PaymentResult(
                existingTx.Id,
                dueId,
                existingTx.Amount,
                existingDue?.RemainingBalance ?? 0,
                existingDue?.Status.ToString() ?? "Unknown"));
        }

        // Load the due with tracking
        var due = await _db.ContributionDues.FirstOrDefaultAsync(d => d.Id == dueId && d.FundId == fundId, ct);
        if (due is null)
            return Result<PaymentResult>.Failure("Contribution due not found.", "NOT_FOUND");

        // Optimistic concurrency check (FR-035a)
        if (due.RowVersion != expectedVersion)
            return Result<PaymentResult>.Failure(
                "The contribution due has been modified. Please refresh and retry.",
                "CONCURRENCY_CONFLICT");

        if (due.Status == ContributionDueStatus.Paid)
            return Result<PaymentResult>.Failure("This due is already fully paid.", "ALREADY_PAID");

        if (due.Status == ContributionDueStatus.Missed)
            return Result<PaymentResult>.Failure("Cannot record payment for a missed due.", "INVALID_STATE");

        if (amount <= 0)
            return Result<PaymentResult>.Failure("Payment amount must be greater than zero.", "VALIDATION_ERROR");

        // Record payment on the due
        var beforeState = new { due.AmountPaid, due.RemainingBalance, Status = due.Status.ToString() };
        var applied = due.RecordPayment(amount);

        // Create ledger transaction
        var tx = Transaction.Create(
            fundId: fundId,
            userId: due.UserId,
            type: TransactionType.Contribution,
            amount: applied,
            idempotencyKey: idempotencyKey,
            recordedBy: recordedBy,
            referenceEntityType: "ContributionDue",
            referenceEntityId: dueId,
            description: description ?? $"Contribution payment for {due.MonthYear}");

        _db.Transactions.Add(tx);
        await _db.SaveChangesAsync(ct);

        // Publish event
        await _publish.Publish(new ContributionPaid(
            Id: Guid.NewGuid(),
            FundId: fundId,
            UserId: due.UserId,
            ContributionDueId: dueId,
            AmountPaid: applied,
            Status: due.Status.ToString(),
            OccurredAt: DateTime.UtcNow), ct);

        await _audit.PublishAsync(
            fundId: fundId,
            actorId: recordedBy,
            entityType: "ContributionDue",
            entityId: dueId,
            actionType: "Payment.Recorded",
            beforeState: beforeState,
            afterState: new { due.AmountPaid, due.RemainingBalance, Status = due.Status.ToString() },
            serviceName: "Contributions",
            cancellationToken: ct);

        return Result<PaymentResult>.Success(new PaymentResult(
            tx.Id,
            dueId,
            applied,
            due.RemainingBalance,
            due.Status.ToString()));
    }
}
