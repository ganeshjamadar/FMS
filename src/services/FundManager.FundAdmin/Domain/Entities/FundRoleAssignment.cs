using FundManager.BuildingBlocks.Domain;

namespace FundManager.FundAdmin.Domain.Entities;

/// <summary>
/// Maps a user to a role within a specific fund.
/// Unique constraint: one role per user per fund.
/// </summary>
public class FundRoleAssignment : Entity
{
    public Guid UserId { get; private set; }
    public Guid FundId { get; private set; }
    public string Role { get; private set; } = string.Empty; // "Admin", "Editor", "Guest"
    public DateTime AssignedAt { get; private set; }
    public Guid AssignedBy { get; private set; }

    // Navigation
    public Fund Fund { get; private set; } = null!;

    private FundRoleAssignment() { } // EF Core

    public static FundRoleAssignment Create(Guid userId, Guid fundId, string role, Guid assignedBy)
    {
        return new FundRoleAssignment
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            FundId = fundId,
            Role = role,
            AssignedAt = DateTime.UtcNow,
            AssignedBy = assignedBy,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }

    public void ChangeRole(string newRole)
    {
        Role = newRole;
        SetUpdated();
    }
}
