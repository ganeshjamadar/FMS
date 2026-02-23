using FundManager.BuildingBlocks.Domain;

namespace FundManager.Identity.Domain.Entities;

/// <summary>
/// User session with sliding expiry (24-hour inactivity timeout per NFR-005).
/// Token is stored as a hash for security.
/// </summary>
public class Session : Entity
{
    public Guid UserId { get; private set; }
    public string TokenHash { get; private set; } = string.Empty;
    public DateTime ExpiresAt { get; private set; }
    public bool Revoked { get; private set; }
    public string? IpAddress { get; private set; }
    public string? UserAgent { get; private set; }

    private static readonly TimeSpan SessionExpiry = TimeSpan.FromHours(24); // NFR-005

    private Session() { } // EF Core

    public static Session Create(Guid userId, string tokenHash, string? ipAddress = null, string? userAgent = null)
    {
        return new Session
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            TokenHash = tokenHash,
            ExpiresAt = DateTime.UtcNow.Add(SessionExpiry),
            Revoked = false,
            IpAddress = ipAddress,
            UserAgent = userAgent,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }

    public bool IsExpired => DateTime.UtcNow > ExpiresAt;

    public bool IsValid => !Revoked && !IsExpired;

    /// <summary>
    /// Extend the session expiry (sliding window).
    /// </summary>
    public void Extend()
    {
        ExpiresAt = DateTime.UtcNow.Add(SessionExpiry);
        SetUpdated();
    }

    /// <summary>
    /// Revoke the session (logout).
    /// </summary>
    public void Revoke()
    {
        Revoked = true;
        SetUpdated();
    }
}
