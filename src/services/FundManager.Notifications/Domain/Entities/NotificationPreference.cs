namespace FundManager.Notifications.Domain.Entities;

/// <summary>
/// User per-channel notification preference. Unique constraint on (UserId, Channel).
/// Enables users to opt-in/out of specific channels (FR-102).
/// </summary>
public class NotificationPreference
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid UserId { get; private set; }

    /// <summary>Channel: push, email, sms, in_app</summary>
    public string Channel { get; private set; } = string.Empty;

    public bool Enabled { get; private set; } = true;
    public DateTime UpdatedAt { get; private set; } = DateTime.UtcNow;

    private NotificationPreference() { }

    public static NotificationPreference Create(Guid userId, string channel, bool enabled = true)
    {
        return new NotificationPreference
        {
            UserId = userId,
            Channel = channel,
            Enabled = enabled,
            UpdatedAt = DateTime.UtcNow,
        };
    }

    public void SetEnabled(bool enabled)
    {
        Enabled = enabled;
        UpdatedAt = DateTime.UtcNow;
    }
}
