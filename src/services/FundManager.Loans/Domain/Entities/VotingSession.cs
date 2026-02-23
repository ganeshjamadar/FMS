using FundManager.BuildingBlocks.Domain;

namespace FundManager.Loans.Domain.Entities;

/// <summary>
/// Voting session result states.
/// Pending → Approved | Rejected | NoQuorum (after finalisation)
/// </summary>
public enum VotingResult
{
    Pending,
    Approved,
    Rejected,
    NoQuorum
}

/// <summary>
/// A voting session for a loan request. At most one session per loan.
/// Fund Admin starts vote, Editors vote within window, Admin finalises with optional override.
/// Concurrency: xmin optimistic locking.
/// </summary>
public class VotingSession : Entity
{
    public Guid LoanId { get; private set; }
    public Guid FundId { get; private set; }
    public DateTime VotingWindowStart { get; private set; }
    public DateTime VotingWindowEnd { get; private set; }
    public string ThresholdType { get; private set; } = "Majority";
    public decimal ThresholdValue { get; private set; } = 50.00m;
    public VotingResult Result { get; private set; } = VotingResult.Pending;
    public Guid? FinalisedBy { get; private set; }
    public DateTime? FinalisedDate { get; private set; }
    public bool OverrideUsed { get; private set; }

    // Navigation — loaded when needed
    public ICollection<Vote> Votes { get; private set; } = new List<Vote>();

    private VotingSession() { } // EF Core

    /// <summary>
    /// Factory: Create a new voting session for a loan.
    /// </summary>
    public static VotingSession Create(
        Guid loanId,
        Guid fundId,
        int votingWindowHours = 48,
        string thresholdType = "Majority",
        decimal thresholdValue = 50.00m)
    {
        if (votingWindowHours < 24 || votingWindowHours > 72)
            throw new ArgumentException("Voting window must be 24–72 hours.", nameof(votingWindowHours));

        var now = DateTime.UtcNow;
        return new VotingSession
        {
            LoanId = loanId,
            FundId = fundId,
            VotingWindowStart = now,
            VotingWindowEnd = now.AddHours(votingWindowHours),
            ThresholdType = thresholdType,
            ThresholdValue = thresholdValue,
            Result = VotingResult.Pending,
            OverrideUsed = false
        };
    }

    /// <summary>
    /// Check if the voting window is currently open.
    /// </summary>
    public bool IsWindowOpen => DateTime.UtcNow >= VotingWindowStart && DateTime.UtcNow <= VotingWindowEnd;

    /// <summary>
    /// Finalise the voting session with a decision.
    /// If the decision contradicts the vote tally, it's an override (FR-049).
    /// </summary>
    public void Finalise(Guid finalisedBy, VotingResult decision, bool isOverride)
    {
        if (Result != VotingResult.Pending)
            throw new InvalidOperationException("Voting session already finalised.");

        Result = decision;
        FinalisedBy = finalisedBy;
        FinalisedDate = DateTime.UtcNow;
        OverrideUsed = isOverride;
        SetUpdated();
    }
}
