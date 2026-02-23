namespace FundManager.BuildingBlocks.Domain;

/// <summary>
/// Base entity with UUID primary key and audit timestamps.
/// All domain entities inherit from this.
/// </summary>
public abstract class Entity
{
    public Guid Id { get; protected set; } = Guid.NewGuid();
    public DateTime CreatedAt { get; protected set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; protected set; } = DateTime.UtcNow;

    /// <summary>
    /// PostgreSQL xmin system column used as optimistic concurrency token.
    /// Mapped via EF Core's UseXminAsConcurrencyToken().
    /// </summary>
    public uint RowVersion { get; set; }

    protected void SetUpdated() => UpdatedAt = DateTime.UtcNow;
}

/// <summary>
/// Aggregate root marker. All repositories should work with aggregate roots only.
/// </summary>
public abstract class AggregateRoot : Entity
{
    private readonly List<IDomainEvent> _domainEvents = [];

    public IReadOnlyList<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    protected void AddDomainEvent(IDomainEvent domainEvent) => _domainEvents.Add(domainEvent);

    public void ClearDomainEvents() => _domainEvents.Clear();
}

/// <summary>
/// Marker interface for domain events (intra-service, in-process).
/// </summary>
public interface IDomainEvent
{
    DateTime OccurredAt { get; }
}
