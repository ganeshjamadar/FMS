using FundManager.BuildingBlocks.Domain;

namespace FundManager.FundAdmin.Domain.Entities;

public enum InvitationStatus
{
    Pending,
    Accepted,
    Declined,
    Expired
}

public class Invitation : Entity
{
    public Guid FundId { get; private set; }
    public string TargetContact { get; private set; } = string.Empty;
    public Guid InvitedBy { get; private set; }
    public InvitationStatus Status { get; private set; }
    public DateTime ExpiresAt { get; private set; }
    public DateTime? RespondedAt { get; private set; }

    // Navigation
    public Fund Fund { get; private set; } = null!;

    private Invitation() { }

    public static Invitation Create(
        Guid fundId,
        string targetContact,
        Guid invitedBy,
        TimeSpan? expiresIn = null)
    {
        if (string.IsNullOrWhiteSpace(targetContact))
            throw new ArgumentException("Target contact is required.", nameof(targetContact));

        return new Invitation
        {
            Id = Guid.NewGuid(),
            FundId = fundId,
            TargetContact = targetContact.Trim(),
            InvitedBy = invitedBy,
            Status = InvitationStatus.Pending,
            ExpiresAt = DateTime.UtcNow.Add(expiresIn ?? TimeSpan.FromDays(7)),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }

    public bool IsExpired => DateTime.UtcNow >= ExpiresAt;

    public Result Accept()
    {
        if (Status != InvitationStatus.Pending)
            return Result.Failure($"Invitation is already {Status}.", "INVALID_STATE");

        if (IsExpired)
        {
            Status = InvitationStatus.Expired;
            UpdatedAt = DateTime.UtcNow;
            return Result.Failure("Invitation has expired.", "EXPIRED");
        }

        Status = InvitationStatus.Accepted;
        RespondedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
        return Result.Success();
    }

    public Result Decline()
    {
        if (Status != InvitationStatus.Pending)
            return Result.Failure($"Invitation is already {Status}.", "INVALID_STATE");

        if (IsExpired)
        {
            Status = InvitationStatus.Expired;
            UpdatedAt = DateTime.UtcNow;
            return Result.Failure("Invitation has expired.", "EXPIRED");
        }

        Status = InvitationStatus.Declined;
        RespondedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
        return Result.Success();
    }

    public void MarkExpired()
    {
        if (Status == InvitationStatus.Pending)
        {
            Status = InvitationStatus.Expired;
            UpdatedAt = DateTime.UtcNow;
        }
    }
}
