using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using FundManager.BuildingBlocks.Auth;
using FundManager.Identity.Domain.Entities;
using FundManager.Identity.Domain.Services;
using FundManager.Identity.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FundManager.Identity.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly OtpService _otpService;
    private readonly JwtTokenService _jwtTokenService;
    private readonly IdentityDbContext _dbContext;

    public AuthController(
        OtpService otpService,
        JwtTokenService jwtTokenService,
        IdentityDbContext dbContext)
    {
        _otpService = otpService;
        _jwtTokenService = jwtTokenService;
        _dbContext = dbContext;
    }

    /// <summary>
    /// POST /api/auth/otp/request — Request an OTP for login.
    /// </summary>
    [HttpPost("otp/request")]
    [AllowAnonymous]
    public async Task<IActionResult> RequestOtp([FromBody] OtpRequestDto request, CancellationToken ct)
    {
        if (request.Channel is not ("phone" or "email"))
            return BadRequest(new ProblemDetails { Title = "Invalid channel", Detail = "Channel must be 'phone' or 'email'." });

        if (string.IsNullOrWhiteSpace(request.Target))
            return BadRequest(new ProblemDetails { Title = "Target required", Detail = "Phone number or email address is required." });

        var result = await _otpService.RequestOtpAsync(request.Channel, request.Target, ct);

        if (!result.IsSuccess)
        {
            if (result.ErrorCode == "RATE_LIMITED")
                return StatusCode(429, new ProblemDetails { Title = "Rate limit exceeded", Detail = result.Error, Status = 429 });
            return BadRequest(new ProblemDetails { Title = "OTP request failed", Detail = result.Error });
        }

        var (challengeId, otp, expiresAt) = result.Value!;

        // In production, the OTP would be sent via SMS/email notification service.
        // For development, we include a masked message.
        var maskedTarget = MaskTarget(request.Channel, request.Target);

        return Accepted(new OtpRequestResponseDto
        {
            ChallengeId = challengeId,
            ExpiresAt = expiresAt,
            Message = $"OTP {otp} sent to {maskedTarget}"
        });
    }

    /// <summary>
    /// POST /api/auth/otp/verify — Verify OTP and obtain session token.
    /// </summary>
    [HttpPost("otp/verify")]
    [AllowAnonymous]
    public async Task<IActionResult> VerifyOtp([FromBody] OtpVerifyRequestDto request, CancellationToken ct)
    {
        var result = await _otpService.VerifyOtpAsync(request.ChallengeId, request.Otp, ct);

        if (!result.IsSuccess)
        {
            return Unauthorized(new ProblemDetails { Title = "Authentication failed", Detail = result.Error, Status = 401 });
        }

        var user = result.Value!;

        // Generate JWT token
        var token = _jwtTokenService.GenerateToken(user.Id, user.Name, user.Email, user.Phone);

        // Create session record
        var tokenHash = HashToken(token);
        var session = Session.Create(
            userId: user.Id,
            tokenHash: tokenHash,
            ipAddress: HttpContext.Connection.RemoteIpAddress?.ToString(),
            userAgent: HttpContext.Request.Headers.UserAgent.ToString());

        _dbContext.Set<Session>().Add(session);
        await _dbContext.SaveChangesAsync(ct);

        return Ok(new AuthResponseDto
        {
            Token = token,
            ExpiresAt = session.ExpiresAt,
            User = MapToProfile(user)
        });
    }

    /// <summary>
    /// POST /api/auth/logout — Revoke current session.
    /// </summary>
    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout(CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (userId is null)
            return Unauthorized();

        // Find the most recent active session for this user
        var session = await _dbContext.Set<Session>()
            .Where(s => s.UserId == userId.Value && !s.Revoked)
            .OrderByDescending(s => s.CreatedAt)
            .FirstOrDefaultAsync(ct);

        if (session is not null)
        {
            session.Revoke();
            await _dbContext.SaveChangesAsync(ct);
        }

        return NoContent();
    }

    private Guid? GetCurrentUserId()
    {
        var sub = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? User.FindFirst("sub")?.Value;
        return sub is not null && Guid.TryParse(sub, out var id) ? id : null;
    }

    private static string HashToken(string token)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static string MaskTarget(string channel, string target)
    {
        if (channel == "phone" && target.Length > 4)
            return new string('*', target.Length - 4) + target[^4..];
        if (channel == "email" && target.Contains('@'))
        {
            var parts = target.Split('@');
            var name = parts[0];
            return (name.Length > 2 ? name[..2] + new string('*', name.Length - 2) : name) + "@" + parts[1];
        }
        return "****";
    }

    private static UserProfileDto MapToProfile(User user) => new()
    {
        Id = user.Id,
        Name = user.Name,
        Phone = user.Phone,
        Email = user.Email,
        ProfilePictureUrl = user.ProfilePictureUrl,
        IsActive = user.IsActive,
        CreatedAt = user.CreatedAt
    };
}

// ── DTOs ────────────────────────────────────────────

public class OtpRequestDto
{
    public string Channel { get; set; } = string.Empty;
    public string Target { get; set; } = string.Empty;
}

public class OtpRequestResponseDto
{
    public Guid ChallengeId { get; set; }
    public DateTime ExpiresAt { get; set; }
    public string Message { get; set; } = string.Empty;
}

public class OtpVerifyRequestDto
{
    public Guid ChallengeId { get; set; }
    public string Otp { get; set; } = string.Empty;
}

public class AuthResponseDto
{
    public string Token { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public UserProfileDto User { get; set; } = null!;
}

public class UserProfileDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? ProfilePictureUrl { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
}
