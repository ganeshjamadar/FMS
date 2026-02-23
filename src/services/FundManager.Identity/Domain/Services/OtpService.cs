using System.Security.Cryptography;
using System.Text;
using FundManager.BuildingBlocks.Domain;
using FundManager.Identity.Domain.Entities;
using FundManager.Identity.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace FundManager.Identity.Domain.Services;

/// <summary>
/// OTP authentication flow: request OTP → verify OTP → issue session.
/// Rate-limited to 5 requests per phone/email per 15 minutes (NFR-006).
/// OTP expires in 5 minutes (NFR-004). Max 3 attempts per challenge.
/// </summary>
public class OtpService
{
    private readonly IdentityDbContext _dbContext;

    private const int OtpLength = 6;
    private const int MaxRequestsPer15Min = 5;

    public OtpService(IdentityDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <summary>
    /// Request an OTP for the given channel and target.
    /// Creates a user if one does not exist.
    /// Returns the challenge ID and the plaintext OTP (for dev/notification purposes).
    /// </summary>
    public async Task<Result<(Guid ChallengeId, string Otp, DateTime ExpiresAt)>> RequestOtpAsync(
        string channel, string target, CancellationToken ct = default)
    {
        // Rate limit check (NFR-006)
        var windowStart = DateTime.UtcNow.AddMinutes(-15);
        var recentCount = await _dbContext.Set<OtpChallenge>()
            .CountAsync(c => c.Target == target && c.CreatedAt >= windowStart, ct);

        if (recentCount >= MaxRequestsPer15Min)
            return Result<(Guid, string, DateTime)>.Failure(
                "Rate limit exceeded. Max 5 OTP requests per 15 minutes.", "RATE_LIMITED");

        // Find or create the user
        var user = channel == "phone"
            ? await _dbContext.Set<User>().FirstOrDefaultAsync(u => u.Phone == target, ct)
            : await _dbContext.Set<User>().FirstOrDefaultAsync(u => u.Email == target, ct);

        if (user is null)
        {
            // Auto-register user on first OTP request
            user = User.Create(
                name: target, // Default name is the contact; user can update profile later
                phone: channel == "phone" ? target : null,
                email: channel == "email" ? target : null);
            _dbContext.Set<User>().Add(user);
        }

        // Generate OTP
        var otp = GenerateOtp();
        var otpHash = HashOtp(otp);

        var challenge = OtpChallenge.Create(user.Id, channel, target, otpHash);
        _dbContext.Set<OtpChallenge>().Add(challenge);

        await _dbContext.SaveChangesAsync(ct);

        return Result<(Guid, string, DateTime)>.Success((challenge.Id, otp, challenge.ExpiresAt));
    }

    /// <summary>
    /// Verify an OTP challenge. On success, returns the user for session/token creation.
    /// </summary>
    public async Task<Result<User>> VerifyOtpAsync(Guid challengeId, string otp, CancellationToken ct = default)
    {
        var challenge = await _dbContext.Set<OtpChallenge>()
            .FirstOrDefaultAsync(c => c.Id == challengeId, ct);

        if (challenge is null)
            return Result<User>.Failure("Challenge not found.", "CHALLENGE_NOT_FOUND");

        if (challenge.Verified)
            return Result<User>.Failure("Challenge already verified.", "ALREADY_VERIFIED");

        if (challenge.IsExpired)
            return Result<User>.Failure("OTP has expired.", "OTP_EXPIRED");

        if (!challenge.RecordAttempt())
            return Result<User>.Failure("Maximum attempts exceeded.", "MAX_ATTEMPTS");

        var otpHash = HashOtp(otp);
        if (challenge.OtpHash != otpHash)
        {
            await _dbContext.SaveChangesAsync(ct); // save the attempt count
            return Result<User>.Failure("Invalid OTP.", "INVALID_OTP");
        }

        challenge.MarkVerified();

        var user = await _dbContext.Set<User>().FirstOrDefaultAsync(u => u.Id == challenge.UserId, ct);
        if (user is null)
            return Result<User>.Failure("User not found.", "USER_NOT_FOUND");

        await _dbContext.SaveChangesAsync(ct);

        return Result<User>.Success(user);
    }

    /// <summary>
    /// Generate a cryptographically secure 6-digit OTP.
    /// </summary>
    private static string GenerateOtp()
    {
        var bytes = RandomNumberGenerator.GetBytes(4);
        var number = BitConverter.ToUInt32(bytes) % 1_000_000;
        return number.ToString("D6");
    }

    /// <summary>
    /// Hash an OTP using SHA-256 for storage comparison.
    /// </summary>
    private static string HashOtp(string otp)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(otp));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
