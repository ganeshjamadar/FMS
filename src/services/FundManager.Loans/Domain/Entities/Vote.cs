using FundManager.BuildingBlocks.Domain;

namespace FundManager.Loans.Domain.Entities;

/// <summary>
/// An individual vote cast by an editor on a loan voting session.
/// Immutable: once cast, a vote cannot be changed (spec 6.3 step 4).
/// Unique: (VotingSessionId, VoterId) — one vote per person per session.
/// </summary>
public class Vote : Entity
{
    public Guid VotingSessionId { get; private set; }
    public Guid VoterId { get; private set; }
    public string Decision { get; private set; } = string.Empty; // "Approve" or "Reject"
    public DateTime CastAt { get; private set; }

    private Vote() { } // EF Core

    /// <summary>
    /// Factory: Create a new vote.
    /// </summary>
    public static Vote Create(
        Guid votingSessionId,
        Guid voterId,
        string decision)
    {
        if (decision != "Approve" && decision != "Reject")
            throw new ArgumentException("Decision must be 'Approve' or 'Reject'.", nameof(decision));

        return new Vote
        {
            VotingSessionId = votingSessionId,
            VoterId = voterId,
            Decision = decision,
            CastAt = DateTime.UtcNow
        };
    }
}
