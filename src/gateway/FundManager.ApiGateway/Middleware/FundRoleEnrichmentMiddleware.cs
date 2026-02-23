using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace FundManager.ApiGateway.Middleware;

/// <summary>
/// Middleware that extracts the fundId from the request path,
/// calls the FundAdmin service to get the current user's role in that fund,
/// and adds it as the X-Fund-Role header before the request is proxied downstream.
/// </summary>
public partial class FundRoleEnrichmentMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<FundRoleEnrichmentMiddleware> _logger;

    public FundRoleEnrichmentMiddleware(
        RequestDelegate next,
        IHttpClientFactory httpClientFactory,
        ILogger<FundRoleEnrichmentMiddleware> logger)
    {
        _next = next;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Only enrich authenticated requests that match /api/funds/{fundId}/...
        if (context.User.Identity?.IsAuthenticated == true)
        {
            var match = FundIdPattern().Match(context.Request.Path.Value ?? string.Empty);
            if (match.Success && Guid.TryParse(match.Groups["fundId"].Value, out _))
            {
                var fundId = match.Groups["fundId"].Value;
                var role = await GetUserFundRoleAsync(context, fundId);
                if (!string.IsNullOrEmpty(role))
                {
                    // Set header for downstream services (YARP proxy)
                    context.Request.Headers["X-Fund-Role"] = role;

                    // Add claim for Gateway's own controllers (runs after auth)
                    if (context.User.Identity is System.Security.Claims.ClaimsIdentity identity
                        && !identity.HasClaim(c => c.Type == "fund_role"))
                    {
                        identity.AddClaim(new System.Security.Claims.Claim("fund_role", role));
                    }
                }
            }
        }

        await _next(context);
    }

    private async Task<string?> GetUserFundRoleAsync(HttpContext context, string fundId)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("fundadmin");

            // Forward the Authorization header to FundAdmin
            var authHeader = context.Request.Headers.Authorization.FirstOrDefault();
            if (!string.IsNullOrEmpty(authHeader))
            {
                client.DefaultRequestHeaders.Authorization =
                    AuthenticationHeaderValue.Parse(authHeader);
            }

            var response = await client.GetAsync($"/api/funds/{fundId}/members/me/role");
            if (!response.IsSuccessStatusCode) return null;

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);

            if (doc.RootElement.TryGetProperty("role", out var roleProp))
            {
                return roleProp.GetString();
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to fetch fund role for fund {FundId}", fundId);
        }

        return null;
    }

    [GeneratedRegex(@"^/api/funds/(?<fundId>[0-9a-fA-F\-]{36})(/|$)")]
    private static partial Regex FundIdPattern();
}
