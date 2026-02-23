using FundManager.BuildingBlocks.Domain;

namespace FundManager.Contributions.Domain.Entities;

public enum TransactionType
{
    Contribution,
    Disbursement,
    Repayment,
    InterestIncome,
    Penalty,
    Settlement
}

/// <summary>
/// Append-only fund ledger entry. Immutable — never updated or deleted.
/// Corrections via reversal entries (Constitution Principle III).
/// Partitioned quarterly by CreatedAt.
/// Unique: (FundId, IdempotencyKey).
/// </summary>
public class Transaction : Entity
{
    public Guid FundId { get; private set; }
    public Guid UserId { get; private set; }
    public TransactionType Type { get; private set; }
    public decimal Amount { get; private set; }
    public string IdempotencyKey { get; private set; } = default!;
    public string? ReferenceEntityType { get; private set; }
    public Guid? ReferenceEntityId { get; private set; }
    public Guid RecordedBy { get; private set; }
    public string? Description { get; private set; }

    private Transaction() { }

    /// <summary>
    /// Factory: Create a new ledger transaction.
    /// </summary>
    public static Transaction Create(
        Guid fundId,
        Guid userId,
        TransactionType type,
        decimal amount,
        string idempotencyKey,
        Guid recordedBy,
        string? referenceEntityType = null,
        Guid? referenceEntityId = null,
        string? description = null)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey))
            throw new ArgumentException("Idempotency key is required.", nameof(idempotencyKey));

        return new Transaction
        {
            FundId = fundId,
            UserId = userId,
            Type = type,
            Amount = amount,
            IdempotencyKey = idempotencyKey,
            RecordedBy = recordedBy,
            ReferenceEntityType = referenceEntityType,
            ReferenceEntityId = referenceEntityId,
            Description = description
        };
    }
}
