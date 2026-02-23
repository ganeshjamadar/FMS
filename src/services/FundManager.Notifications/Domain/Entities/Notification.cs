using System.Text.Json;

namespace FundManager.Notifications.Domain.Entities;

/// <summary>
/// Represents a notification to be delivered to a user via one or more channels.
/// Supports retry with exponential backoff and channel fallback (push → email → in-app).
/// </summary>
public class Notification
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid RecipientId { get; private set; }
    public Guid? FundId { get; private set; }

    /// <summary>Channel: push, email, sms, in_app</summary>
    public string Channel { get; private set; } = string.Empty;

    /// <summary>Template key e.g. 'contribution.due.generated'</summary>
    public string TemplateKey { get; private set; } = string.Empty;

    /// <summary>Template substitution values stored as JSONB.</summary>
    public JsonDocument Placeholders { get; private set; } = JsonDocument.Parse("{}");

    /// <summary>Status: Pending, Sent, Failed</summary>
    public string Status { get; private set; } = "Pending";

    public int RetryCount { get; private set; }
    public int MaxRetries { get; private set; } = 3;
    public DateTime? NextRetryAt { get; private set; }
    public DateTime ScheduledAt { get; private set; } = DateTime.UtcNow;
    public DateTime? SentAt { get; private set; }
    public DateTime? FailedAt { get; private set; }
    public string? FailureReason { get; private set; }
    public DateTime CreatedAt { get; private set; } = DateTime.UtcNow;

    private Notification() { }

    public static Notification Create(
        Guid recipientId,
        Guid? fundId,
        string channel,
        string templateKey,
        JsonDocument placeholders,
        DateTime? scheduledAt = null)
    {
        return new Notification
        {
            RecipientId = recipientId,
            FundId = fundId,
            Channel = channel,
            TemplateKey = templateKey,
            Placeholders = placeholders,
            ScheduledAt = scheduledAt ?? DateTime.UtcNow,
        };
    }

    /// <summary>Mark notification as successfully sent.</summary>
    public void MarkSent()
    {
        Status = "Sent";
        SentAt = DateTime.UtcNow;
        NextRetryAt = null;
    }

    /// <summary>
    /// Schedule a retry with exponential backoff.
    /// Returns false if max retries exceeded.
    /// </summary>
    public bool ScheduleRetry(string reason)
    {
        RetryCount++;
        if (RetryCount >= MaxRetries)
        {
            MarkFailed(reason);
            return false;
        }

        // Exponential backoff: 2^retryCount minutes
        var delay = TimeSpan.FromMinutes(Math.Pow(2, RetryCount));
        NextRetryAt = DateTime.UtcNow.Add(delay);
        FailureReason = reason;
        return true;
    }

    /// <summary>Mark notification as failed (all retries exhausted).</summary>
    public void MarkFailed(string reason)
    {
        Status = "Failed";
        FailedAt = DateTime.UtcNow;
        FailureReason = reason;
        NextRetryAt = null;
    }

    /// <summary>Change channel for fallback delivery (push → email → in_app).</summary>
    public void FallbackToChannel(string newChannel)
    {
        Channel = newChannel;
        RetryCount = 0;
        NextRetryAt = null;
        FailureReason = null;
    }
}
