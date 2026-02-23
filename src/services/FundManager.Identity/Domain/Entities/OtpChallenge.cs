using FundManager.BuildingBlocks.Domain;

namespace FundManager.Identity.Domain.Entities;

/// <summary>
/// One-time password challenge for passwordless authentication.
/// OTP expires after 5 minutes (NFR-004). Max 3 attempts per challenge.
/// </summary>
public class OtpChallenge : Entity
{
    public Guid UserId { get; private set; }
    public string Channel { get; private set; } = string.Empty; // "phone" or "email"
    public string Target { get; private set; } = string.Empty;  // Phone number or email address
    public string OtpHash { get; private set; } = string.Empty; // BCrypt or SHA-256 hash of the OTP
    public DateTime ExpiresAt { get; private set; }
    public bool Verified { get; private set; }
    public int Attempts { get; private set; }

    private const int MaxAttempts = 3;
    private static readonly TimeSpan OtpExpiry = TimeSpan.FromMinutes(5); // NFR-004

    private OtpChallenge() { } // EF Core

    public static OtpChallenge Create(Guid userId, string channel, string target, string otpHash)
    {
        return new OtpChallenge
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Channel = channel,
            Target = target,
            OtpHash = otpHash,
            ExpiresAt = DateTime.UtcNow.Add(OtpExpiry),
            Verified = false,
            Attempts = 0,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }

    public bool IsExpired => DateTime.UtcNow > ExpiresAt;

    public bool HasExceededMaxAttempts => Attempts >= MaxAttempts;

    /// <summary>
    /// Record an attempt. Returns true if attempt is allowed.
    /// </summary>
    public bool RecordAttempt()
    {
        if (Verified || IsExpired || HasExceededMaxAttempts)
            return false;

        Attempts++;
        SetUpdated();
        return true;
    }

    /// <summary>
    /// Mark the challenge as verified.
    /// </summary>
    public void MarkVerified()
    {
        Verified = true;
        SetUpdated();
    }
}
