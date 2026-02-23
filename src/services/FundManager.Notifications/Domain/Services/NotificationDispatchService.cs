using System.Text.Json;
using FundManager.Notifications.Domain.Entities;
using FundManager.Notifications.Infrastructure.Data;
using FundManager.Notifications.Infrastructure.Templates;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FundManager.Notifications.Domain.Services;

/// <summary>
/// Central dispatch service for notifications.
/// Checks preferences, selects channel, sends via provider, manages retry (3 attempts
/// with exponential backoff) and fallback (push → email → in_app) per FR-100, FR-104, FR-105.
/// </summary>
public class NotificationDispatchService
{
    private readonly NotificationsDbContext _db;
    private readonly NotificationTemplateEngine _templateEngine;
    private readonly ILogger<NotificationDispatchService> _logger;

    // Channel priority for fallback: push → email → in_app
    private static readonly string[] ChannelFallbackOrder = ["push", "email", "in_app"];

    public NotificationDispatchService(
        NotificationsDbContext db,
        NotificationTemplateEngine templateEngine,
        ILogger<NotificationDispatchService> logger)
    {
        _db = db;
        _templateEngine = templateEngine;
        _logger = logger;
    }

    /// <summary>
    /// Dispatch a notification to the recipient.
    /// Creates notification records for each enabled channel (fallback order).
    /// </summary>
    public async Task DispatchAsync(
        Guid recipientId,
        Guid? fundId,
        string templateKey,
        Dictionary<string, string> placeholders,
        CancellationToken ct = default)
    {
        var preferences = await _db.NotificationPreferences
            .Where(p => p.UserId == recipientId)
            .ToListAsync(ct);

        var placeholderDoc = JsonDocument.Parse(JsonSerializer.Serialize(placeholders));

        // Determine which channels are enabled for this user
        var enabledChannels = GetEnabledChannels(preferences);

        if (enabledChannels.Count == 0)
        {
            _logger.LogInformation(
                "No enabled channels for recipient {RecipientId}, template {TemplateKey}. Defaulting to in_app.",
                recipientId, templateKey);
            enabledChannels = ["in_app"];
        }

        // Create a notification for the highest-priority enabled channel
        var primaryChannel = enabledChannels[0];
        var notification = Notification.Create(
            recipientId: recipientId,
            fundId: fundId,
            channel: primaryChannel,
            templateKey: templateKey,
            placeholders: placeholderDoc);

        _db.Notifications.Add(notification);

        // Always create an in_app notification for the feed (unless already in_app)
        if (primaryChannel != "in_app")
        {
            var inAppNotification = Notification.Create(
                recipientId: recipientId,
                fundId: fundId,
                channel: "in_app",
                templateKey: templateKey,
                placeholders: placeholderDoc);
            // in_app notifications are always immediately "sent"
            inAppNotification.MarkSent();
            _db.Notifications.Add(inAppNotification);
        }

        await _db.SaveChangesAsync(ct);

        // Attempt delivery for the primary channel
        await AttemptDeliveryAsync(notification, ct);
    }

    /// <summary>
    /// Attempt to deliver a notification. On failure, retry or fallback.
    /// </summary>
    public async Task AttemptDeliveryAsync(Notification notification, CancellationToken ct = default)
    {
        try
        {
            var (title, body) = _templateEngine.Render(notification.TemplateKey, notification.Placeholders);

            var sent = await SendViaChannelAsync(notification.Channel, notification.RecipientId, title, body, ct);

            if (sent)
            {
                notification.MarkSent();
                _logger.LogInformation(
                    "Notification {NotificationId} sent via {Channel} to {RecipientId}",
                    notification.Id, notification.Channel, notification.RecipientId);
            }
            else
            {
                HandleDeliveryFailure(notification, "Delivery provider returned failure");
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Notification {NotificationId} delivery failed via {Channel}",
                notification.Id, notification.Channel);
            HandleDeliveryFailure(notification, ex.Message);
        }

        await _db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Process pending notifications that are due for retry.
    /// </summary>
    public async Task ProcessRetryQueueAsync(CancellationToken ct = default)
    {
        var pendingRetries = await _db.Notifications
            .Where(n => n.Status == "Pending"
                        && n.NextRetryAt != null
                        && n.NextRetryAt <= DateTime.UtcNow)
            .OrderBy(n => n.NextRetryAt)
            .Take(50)
            .ToListAsync(ct);

        foreach (var notification in pendingRetries)
        {
            await AttemptDeliveryAsync(notification, ct);
        }
    }

    private void HandleDeliveryFailure(Notification notification, string reason)
    {
        var retryScheduled = notification.ScheduleRetry(reason);

        if (!retryScheduled)
        {
            // All retries exhausted — try fallback channel
            var nextChannel = GetNextFallbackChannel(notification.Channel);
            if (nextChannel is not null)
            {
                _logger.LogInformation(
                    "Notification {NotificationId} falling back from {OldChannel} to {NewChannel}",
                    notification.Id, notification.Channel, nextChannel);
                notification.FallbackToChannel(nextChannel);
            }
            else
            {
                _logger.LogWarning(
                    "Notification {NotificationId} failed — all channels exhausted",
                    notification.Id);
            }
        }
    }

    private static string? GetNextFallbackChannel(string currentChannel)
    {
        var currentIndex = Array.IndexOf(ChannelFallbackOrder, currentChannel);
        if (currentIndex < 0 || currentIndex >= ChannelFallbackOrder.Length - 1)
            return null;
        return ChannelFallbackOrder[currentIndex + 1];
    }

    private List<string> GetEnabledChannels(List<NotificationPreference> preferences)
    {
        var enabled = new List<string>();
        foreach (var channel in ChannelFallbackOrder)
        {
            var pref = preferences.FirstOrDefault(p => p.Channel == channel);
            // If no preference exists for a channel, default to enabled
            if (pref is null || pref.Enabled)
            {
                enabled.Add(channel);
            }
        }
        return enabled;
    }

    /// <summary>
    /// Simulated channel delivery. In production, this would integrate with
    /// FCM/APNs for push, SendGrid/SES for email, Twilio for SMS.
    /// </summary>
    private Task<bool> SendViaChannelAsync(
        string channel, Guid recipientId, string title, string body, CancellationToken ct)
    {
        _logger.LogInformation(
            "Sending {Channel} notification to {RecipientId}: {Title}",
            channel, recipientId, title);

        // Simulated — always succeeds for in_app, simulates success for others
        return Task.FromResult(true);
    }
}
