using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace FundManager.BuildingBlocks.Auth;

/// <summary>
/// JWT token generation and validation for OTP-based authentication.
/// Shared by all services for consistent token handling.
/// </summary>
public class JwtTokenService
{
    private readonly string _secret;
    private readonly int _expiryHours;
    private readonly string _issuer;

    public JwtTokenService(IConfiguration configuration)
    {
        _secret = configuration["Jwt:Secret"]
            ?? throw new InvalidOperationException("JWT secret not configured");
        _expiryHours = int.TryParse(configuration["Jwt:ExpiryHours"], out var hours) ? hours : 24;
        _issuer = configuration["Jwt:Issuer"] ?? "FundManager";
    }

    /// <summary>
    /// Generate a JWT token for an authenticated user.
    /// </summary>
    public string GenerateToken(Guid userId, string name, string? email, string? phone)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_secret));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new(JwtRegisteredClaimNames.Name, name),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new(JwtRegisteredClaimNames.Iat, DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64),
        };

        if (!string.IsNullOrEmpty(email))
            claims.Add(new Claim(JwtRegisteredClaimNames.Email, email));
        if (!string.IsNullOrEmpty(phone))
            claims.Add(new Claim("phone", phone));

        var token = new JwtSecurityToken(
            issuer: _issuer,
            audience: _issuer,
            claims: claims,
            expires: DateTime.UtcNow.AddHours(_expiryHours),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    /// <summary>
    /// Get TokenValidationParameters for configuring JWT authentication.
    /// </summary>
    public TokenValidationParameters GetValidationParameters()
    {
        return new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = _issuer,
            ValidateAudience = true,
            ValidAudience = _issuer,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_secret)),
            ClockSkew = TimeSpan.FromMinutes(1)
        };
    }
}
