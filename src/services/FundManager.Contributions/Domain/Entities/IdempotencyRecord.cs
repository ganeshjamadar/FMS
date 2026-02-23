using FundManager.BuildingBlocks.Domain;

namespace FundManager.Contributions.Domain.Entities;

/// <summary>
/// Tracks idempotency keys for de-duplication (FR-114, NFR-010).
/// Unique: (FundId, IdempotencyKey, Endpoint). 90-day retention.
/// </summary>
public class IdempotencyRecord : Entity
{
    public string IdempotencyKey { get; private set; } = default!;
    public Guid FundId { get; private set; }
    public string Endpoint { get; private set; } = default!;
    public string RequestHash { get; private set; } = default!;
    public int StatusCode { get; private set; }
    public string ResponseBody { get; private set; } = default!;
    public DateTime ExpiresAt { get; private set; }

    private IdempotencyRecord() { }

    public static IdempotencyRecord Create(
        Guid fundId,
        string idempotencyKey,
        string endpoint,
        string requestHash,
        int statusCode,
        string responseBody)
    {
        return new IdempotencyRecord
        {
            FundId = fundId,
            IdempotencyKey = idempotencyKey,
            Endpoint = endpoint,
            RequestHash = requestHash,
            StatusCode = statusCode,
            ResponseBody = responseBody,
            ExpiresAt = DateTime.UtcNow.AddDays(90)
        };
    }
}
