namespace FundManager.Notifications.Domain.Entities;

/// <summary>
/// Push notification device registration. Unique constraint on (UserId, DeviceId).
/// </summary>
public class DeviceToken
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid UserId { get; private set; }

    /// <summary>Client-assigned device identifier.</summary>
    public string DeviceId { get; private set; } = string.Empty;

    /// <summary>Platform push token (APNs / FCM).</summary>
    public string PushToken { get; private set; } = string.Empty;

    /// <summary>Platform: ios or android.</summary>
    public string Platform { get; private set; } = string.Empty;

    public DateTime UpdatedAt { get; private set; } = DateTime.UtcNow;

    private DeviceToken() { }

    public static DeviceToken Create(Guid userId, string deviceId, string pushToken, string platform)
    {
        return new DeviceToken
        {
            UserId = userId,
            DeviceId = deviceId,
            PushToken = pushToken,
            Platform = platform,
            UpdatedAt = DateTime.UtcNow,
        };
    }

    public void UpdatePushToken(string pushToken)
    {
        PushToken = pushToken;
        UpdatedAt = DateTime.UtcNow;
    }
}
