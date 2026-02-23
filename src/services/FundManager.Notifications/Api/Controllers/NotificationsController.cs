using System.Security.Claims;
using FundManager.Notifications.Domain.Entities;
using FundManager.Notifications.Infrastructure.Data;
using FundManager.Notifications.Infrastructure.Templates;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FundManager.Notifications.Api.Controllers;

[ApiController]
[Route("api/notifications")]
[Authorize]
public class NotificationsController : ControllerBase
{
    private readonly NotificationsDbContext _db;
    private readonly NotificationTemplateEngine _templateEngine;

    public NotificationsController(
        NotificationsDbContext db,
        NotificationTemplateEngine templateEngine)
    {
        _db = db;
        _templateEngine = templateEngine;
    }

    // ── GET /api/notifications/feed ──────────────────────

    /// <summary>
    /// Get current user's notification feed, optionally filtered by fund and status.
    /// </summary>
    [HttpGet("feed")]
    public async Task<IActionResult> GetFeed(
        [FromQuery] Guid? fundId,
        [FromQuery] string? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var userId = GetCurrentUserId();
        if (userId is null) return Unauthorized();

        var query = _db.Notifications
            .AsNoTracking()
            .Where(n => n.RecipientId == userId.Value && n.Channel == "in_app");

        if (fundId.HasValue)
            query = query.Where(n => n.FundId == fundId.Value);

        if (!string.IsNullOrEmpty(status))
            query = query.Where(n => n.Status == status);

        var totalCount = await query.CountAsync(ct);

        var items = await query
            .OrderByDescending(n => n.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        var feedItems = items.Select(n =>
        {
            var (title, body) = _templateEngine.Render(n.TemplateKey, n.Placeholders);
            return new NotificationFeedItemDto
            {
                Id = n.Id,
                FundId = n.FundId,
                Channel = n.Channel,
                TemplateKey = n.TemplateKey,
                Title = title,
                Body = body,
                Status = n.Status,
                ScheduledAt = n.ScheduledAt,
                SentAt = n.SentAt,
            };
        }).ToList();

        return Ok(new PaginatedFeedDto
        {
            Items = feedItems,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize,
        });
    }

    // ── GET /api/notifications/feed/unread-count ─────────

    /// <summary>
    /// Get count of unread/pending in-app notifications.
    /// </summary>
    [HttpGet("feed/unread-count")]
    public async Task<IActionResult> GetUnreadCount(CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (userId is null) return Unauthorized();

        var count = await _db.Notifications
            .CountAsync(n => n.RecipientId == userId.Value
                            && n.Channel == "in_app"
                            && n.Status == "Pending",
                ct);

        return Ok(new { count });
    }

    // ── GET /api/notifications/preferences ───────────────

    /// <summary>
    /// Get current user's notification preferences.
    /// </summary>
    [HttpGet("preferences")]
    public async Task<IActionResult> GetPreferences(CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (userId is null) return Unauthorized();

        var prefs = await _db.NotificationPreferences
            .AsNoTracking()
            .Where(p => p.UserId == userId.Value)
            .ToListAsync(ct);

        // If no preferences exist, return defaults (all channels enabled)
        if (prefs.Count == 0)
        {
            prefs = CreateDefaultPreferences(userId.Value);
        }

        return Ok(prefs.Select(p => new PreferenceDto
        {
            Channel = p.Channel,
            Enabled = p.Enabled,
        }));
    }

    // ── PUT /api/notifications/preferences ───────────────

    /// <summary>
    /// Update notification preferences (FR-102).
    /// </summary>
    [HttpPut("preferences")]
    public async Task<IActionResult> UpdatePreferences(
        [FromBody] List<UpdatePreferenceDto> requests,
        CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (userId is null) return Unauthorized();

        var existing = await _db.NotificationPreferences
            .Where(p => p.UserId == userId.Value)
            .ToListAsync(ct);

        foreach (var req in requests)
        {
            var pref = existing.FirstOrDefault(p => p.Channel == req.Channel);
            if (pref is not null)
            {
                pref.SetEnabled(req.Enabled);
            }
            else
            {
                var newPref = NotificationPreference.Create(userId.Value, req.Channel, req.Enabled);
                _db.NotificationPreferences.Add(newPref);
                existing.Add(newPref);
            }
        }

        await _db.SaveChangesAsync(ct);

        return Ok(existing.Select(p => new PreferenceDto
        {
            Channel = p.Channel,
            Enabled = p.Enabled,
        }));
    }

    // ── POST /api/notifications/devices ──────────────────

    /// <summary>
    /// Register a device for push notifications.
    /// </summary>
    [HttpPost("devices")]
    public async Task<IActionResult> RegisterDevice(
        [FromBody] RegisterDeviceDto request,
        CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (userId is null) return Unauthorized();

        var existing = await _db.DeviceTokens
            .FirstOrDefaultAsync(d => d.UserId == userId.Value && d.DeviceId == request.DeviceId, ct);

        if (existing is not null)
        {
            existing.UpdatePushToken(request.PushToken);
        }
        else
        {
            var device = DeviceToken.Create(userId.Value, request.DeviceId, request.PushToken, request.Platform);
            _db.DeviceTokens.Add(device);
        }

        await _db.SaveChangesAsync(ct);
        return Ok();
    }

    // ── DELETE /api/notifications/devices ─────────────────

    /// <summary>
    /// Unregister a device token.
    /// </summary>
    [HttpDelete("devices")]
    public async Task<IActionResult> UnregisterDevice(
        [FromBody] UnregisterDeviceDto request,
        CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (userId is null) return Unauthorized();

        var device = await _db.DeviceTokens
            .FirstOrDefaultAsync(d => d.UserId == userId.Value && d.DeviceId == request.DeviceId, ct);

        if (device is not null)
        {
            _db.DeviceTokens.Remove(device);
            await _db.SaveChangesAsync(ct);
        }

        return NoContent();
    }

    // ── Helpers ──────────────────────────────────────────

    private Guid? GetCurrentUserId()
    {
        var sub = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? User.FindFirst("sub")?.Value;
        return sub is not null && Guid.TryParse(sub, out var id) ? id : null;
    }

    private static List<NotificationPreference> CreateDefaultPreferences(Guid userId)
    {
        return
        [
            NotificationPreference.Create(userId, "push"),
            NotificationPreference.Create(userId, "email"),
            NotificationPreference.Create(userId, "sms"),
            NotificationPreference.Create(userId, "in_app"),
        ];
    }
}

// ── DTOs ─────────────────────────────────────────────

public class NotificationFeedItemDto
{
    public Guid Id { get; set; }
    public Guid? FundId { get; set; }
    public string? FundName { get; set; }
    public string Channel { get; set; } = string.Empty;
    public string TemplateKey { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime ScheduledAt { get; set; }
    public DateTime? SentAt { get; set; }
}

public class PaginatedFeedDto
{
    public List<NotificationFeedItemDto> Items { get; set; } = [];
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
}

public class PreferenceDto
{
    public string Channel { get; set; } = string.Empty;
    public bool Enabled { get; set; }
}

public class UpdatePreferenceDto
{
    public string Channel { get; set; } = string.Empty;
    public bool Enabled { get; set; }
}

public class RegisterDeviceDto
{
    public string DeviceId { get; set; } = string.Empty;
    public string PushToken { get; set; } = string.Empty;
    public string Platform { get; set; } = string.Empty;
}

public class UnregisterDeviceDto
{
    public string DeviceId { get; set; } = string.Empty;
}
