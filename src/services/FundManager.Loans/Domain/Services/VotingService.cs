using FundManager.BuildingBlocks.Audit;
using FundManager.BuildingBlocks.Domain;
using FundManager.Contracts.Events;
using FundManager.Loans.Domain.Entities;
using FundManager.Loans.Infrastructure.Data;
using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace FundManager.Loans.Domain.Services;

/// <summary>
/// Manages the loan voting workflow: start session, cast votes, finalise.
/// One voting session per loan. Editors vote Approve/Reject (immutable).
/// Admin finalises with optional override (FR-044 through FR-049).
/// </summary>
public class VotingService
{
    private readonly LoansDbContext _db;
    private readonly IPublishEndpoint _publishEndpoint;
    private readonly AuditEventPublisher _audit;

    public VotingService(
        LoansDbContext db,
        IPublishEndpoint publishEndpoint,
        AuditEventPublisher audit)
    {
        _db = db;
        _publishEndpoint = publishEndpoint;
        _audit = audit;
    }

    /// <summary>
    /// Start a voting session for a loan in PendingApproval status.
    /// Only one session per loan allowed.
    /// </summary>
    public async Task<Result<VotingSession>> StartVotingAsync(
        Guid fundId,
        Guid loanId,
        Guid startedBy,
        int votingWindowHours = 48,
        string thresholdType = "Majority",
        decimal thresholdValue = 50.00m,
        CancellationToken ct = default)
    {
        // Verify loan exists and is in PendingApproval
        var loan = await _db.Loans
            .AsNoTracking()
            .FirstOrDefaultAsync(l => l.Id == loanId && l.FundId == fundId, ct);

        if (loan is null)
            return Result<VotingSession>.Failure("Loan not found.", "NOT_FOUND");

        if (loan.Status != LoanStatus.PendingApproval)
            return Result<VotingSession>.Failure(
                $"Cannot start voting on loan in {loan.Status} status.",
                "INVALID_STATUS");

        // Check no existing session
        var existing = await _db.VotingSessions
            .AnyAsync(v => v.LoanId == loanId, ct);

        if (existing)
            return Result<VotingSession>.Failure(
                "A voting session already exists for this loan.",
                "ALREADY_EXISTS");

        var session = VotingSession.Create(
            loanId, fundId, votingWindowHours, thresholdType, thresholdValue);

        _db.VotingSessions.Add(session);
        await _db.SaveChangesAsync(ct);

        // Publish VotingStarted
        await _publishEndpoint.Publish(new VotingStarted(
            Id: Guid.NewGuid(),
            FundId: fundId,
            LoanId: loanId,
            VotingSessionId: session.Id,
            WindowEnd: session.VotingWindowEnd,
            OccurredAt: DateTime.UtcNow), ct);

        await _audit.PublishAsync(
            fundId: fundId,
            actorId: startedBy,
            entityType: "VotingSession",
            entityId: session.Id,
            actionType: "VotingStarted",
            beforeState: null,
            afterState: session,
            serviceName: "FundManager.Loans",
            cancellationToken: ct);

        return Result<VotingSession>.Success(session);
    }

    /// <summary>
    /// Cast a vote. One per editor per session. Immutable.
    /// </summary>
    public async Task<Result<Vote>> CastVoteAsync(
        Guid fundId,
        Guid loanId,
        Guid voterId,
        string decision,
        CancellationToken ct = default)
    {
        var session = await _db.VotingSessions
            .FirstOrDefaultAsync(v => v.LoanId == loanId && v.FundId == fundId, ct);

        if (session is null)
            return Result<Vote>.Failure("No voting session found for this loan.", "NOT_FOUND");

        if (session.Result != VotingResult.Pending)
            return Result<Vote>.Failure("Voting session already finalised.", "ALREADY_FINALISED");

        if (!session.IsWindowOpen)
            return Result<Vote>.Failure("Voting window is closed.", "WINDOW_CLOSED");

        // Check if already voted
        var alreadyVoted = await _db.Votes
            .AnyAsync(v => v.VotingSessionId == session.Id && v.VoterId == voterId, ct);

        if (alreadyVoted)
            return Result<Vote>.Failure("You have already voted.", "ALREADY_VOTED");

        if (decision != "Approve" && decision != "Reject")
            return Result<Vote>.Failure("Decision must be 'Approve' or 'Reject'.", "VALIDATION");

        var vote = Vote.Create(session.Id, voterId, decision);
        _db.Votes.Add(vote);
        await _db.SaveChangesAsync(ct);

        // Publish VoteCast
        await _publishEndpoint.Publish(new VoteCast(
            Id: Guid.NewGuid(),
            FundId: fundId,
            VotingSessionId: session.Id,
            VoterId: voterId,
            Decision: decision,
            OccurredAt: DateTime.UtcNow), ct);

        return Result<Vote>.Success(vote);
    }

    /// <summary>
    /// Finalise voting session. Admin decides Approve/Reject.
    /// If decision contradicts tally, it's an override logged in audit (FR-049).
    /// </summary>
    public async Task<Result<VotingSession>> FinaliseVotingAsync(
        Guid fundId,
        Guid loanId,
        Guid finalisedBy,
        string decision,
        CancellationToken ct = default)
    {
        var session = await _db.VotingSessions
            .Include(v => v.Votes)
            .FirstOrDefaultAsync(v => v.LoanId == loanId && v.FundId == fundId, ct);

        if (session is null)
            return Result<VotingSession>.Failure("No voting session found for this loan.", "NOT_FOUND");

        if (session.Result != VotingResult.Pending)
            return Result<VotingSession>.Failure("Voting session already finalised.", "ALREADY_FINALISED");

        if (decision != "Approve" && decision != "Reject")
            return Result<VotingSession>.Failure("Decision must be 'Approve' or 'Reject'.", "VALIDATION");

        // Calculate tally
        var approveCount = session.Votes.Count(v => v.Decision == "Approve");
        var rejectCount = session.Votes.Count(v => v.Decision == "Reject");
        var totalVotes = approveCount + rejectCount;

        // Determine natural outcome based on threshold
        VotingResult naturalOutcome;
        if (totalVotes == 0)
        {
            naturalOutcome = VotingResult.NoQuorum;
        }
        else if (session.ThresholdType == "Majority")
        {
            naturalOutcome = approveCount > rejectCount
                ? VotingResult.Approved
                : VotingResult.Rejected;
        }
        else // Percentage
        {
            var approvePercent = (decimal)approveCount / totalVotes * 100;
            naturalOutcome = approvePercent >= session.ThresholdValue
                ? VotingResult.Approved
                : VotingResult.Rejected;
        }

        var adminDecision = decision == "Approve" ? VotingResult.Approved : VotingResult.Rejected;
        var isOverride = naturalOutcome != VotingResult.NoQuorum && adminDecision != naturalOutcome;

        var beforeState = new { session.Result, ApproveCount = approveCount, RejectCount = rejectCount };

        session.Finalise(finalisedBy, adminDecision, isOverride);
        await _db.SaveChangesAsync(ct);

        // Publish VotingFinalised
        await _publishEndpoint.Publish(new VotingFinalised(
            Id: Guid.NewGuid(),
            FundId: fundId,
            VotingSessionId: session.Id,
            LoanId: loanId,
            Result: adminDecision.ToString(),
            OverrideUsed: isOverride,
            OccurredAt: DateTime.UtcNow), ct);

        await _audit.PublishAsync(
            fundId: fundId,
            actorId: finalisedBy,
            entityType: "VotingSession",
            entityId: session.Id,
            actionType: isOverride ? "VotingFinalisedWithOverride" : "VotingFinalised",
            beforeState: beforeState,
            afterState: new
            {
                session.Result,
                ApproveCount = approveCount,
                RejectCount = rejectCount,
                OverrideUsed = isOverride
            },
            serviceName: "FundManager.Loans",
            cancellationToken: ct);

        return Result<VotingSession>.Success(session);
    }

    /// <summary>
    /// Get voting session with votes for a loan.
    /// </summary>
    public async Task<VotingSession?> GetVotingSessionAsync(
        Guid fundId,
        Guid loanId,
        CancellationToken ct = default)
    {
        return await _db.VotingSessions
            .AsNoTracking()
            .Include(v => v.Votes)
            .FirstOrDefaultAsync(v => v.LoanId == loanId && v.FundId == fundId, ct);
    }
}
