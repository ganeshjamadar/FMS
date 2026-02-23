using System.Security.Claims;
using FundManager.Identity.Domain.Entities;
using FundManager.Identity.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FundManager.Identity.Api.Controllers;

[ApiController]
[Route("api/profile")]
[Authorize]
public class ProfileController : ControllerBase
{
    private readonly IdentityDbContext _dbContext;

    public ProfileController(IdentityDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <summary>
    /// GET /api/profile/me — Get current user's profile.
    /// </summary>
    [HttpGet("me")]
    public async Task<IActionResult> GetProfile(CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (userId is null) return Unauthorized();

        var user = await _dbContext.Set<User>()
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == userId.Value, ct);

        if (user is null) return NotFound();

        return Ok(MapToProfile(user));
    }

    /// <summary>
    /// PUT /api/profile/me — Update current user's profile.
    /// </summary>
    [HttpPut("me")]
    public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileRequestDto request, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (userId is null) return Unauthorized();

        var user = await _dbContext.Set<User>()
            .FirstOrDefaultAsync(u => u.Id == userId.Value, ct);

        if (user is null) return NotFound();

        try
        {
            user.UpdateProfile(request.Name, request.Phone, request.Email, request.ProfilePictureUrl);
            await _dbContext.SaveChangesAsync(ct);
            return Ok(MapToProfile(user));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Validation error",
                Detail = ex.Message,
                Status = 400
            });
        }
    }

    /// <summary>
    /// GET /api/profile/me/funds — List all funds the current user belongs to.
    /// NOTE: This queries the FundAdmin schema cross-service. In the microservice architecture,
    /// this would call the FundAdmin service via HTTP or use an event-sourced local projection.
    /// For now, we return an empty list — the actual implementation will be wired
    /// when the FundAdmin MemberJoined consumer creates a local projection.
    /// </summary>
    [HttpGet("me/funds")]
    public Task<IActionResult> GetMyFunds(CancellationToken ct)
    {
        // Cross-service: would query fundadmin.fund_role_assignments
        // For now, return empty list placeholder until cross-service projection is built
        return Task.FromResult<IActionResult>(Ok(Array.Empty<FundMembershipDto>()));
    }

    private Guid? GetCurrentUserId()
    {
        var sub = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? User.FindFirst("sub")?.Value;
        return sub is not null && Guid.TryParse(sub, out var id) ? id : null;
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

public class UpdateProfileRequestDto
{
    public string? Name { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? ProfilePictureUrl { get; set; }
}

public class FundMembershipDto
{
    public Guid FundId { get; set; }
    public string FundName { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string FundState { get; set; } = string.Empty;
}
