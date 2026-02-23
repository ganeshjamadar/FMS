using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;

namespace FundManager.BuildingBlocks.Auth;

/// <summary>
/// Reads the X-Fund-Role header (set by the API Gateway) and adds it as
/// a "fund_role" claim so that authorization policies can evaluate it.
/// </summary>
public class FundRoleClaimsTransformation : IClaimsTransformation
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public FundRoleClaimsTransformation(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
    {
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext is null) return Task.FromResult(principal);

        // Already has fund_role claim — skip
        if (principal.HasClaim(c => c.Type == "fund_role"))
            return Task.FromResult(principal);

        var fundRole = httpContext.Request.Headers["X-Fund-Role"].FirstOrDefault();
        if (string.IsNullOrEmpty(fundRole))
            return Task.FromResult(principal);

        // Clone the identity and add the fund_role claim
        var identity = principal.Identity as ClaimsIdentity;
        if (identity is not null)
        {
            identity.AddClaim(new Claim("fund_role", fundRole));
        }

        return Task.FromResult(principal);
    }
}
