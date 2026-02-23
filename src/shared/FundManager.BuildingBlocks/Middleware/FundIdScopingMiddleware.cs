using Microsoft.AspNetCore.Http;

namespace FundManager.BuildingBlocks.Middleware;

/// <summary>
/// Extracts fundId from route values or headers and sets it on HttpContext.Items.
/// All fund-scoped operations must have a fundId available via this middleware.
/// Constitution Principle I: Multi-Fund Segregation Is Mandatory.
/// </summary>
public class FundIdScopingMiddleware
{
    private readonly RequestDelegate _next;
    public const string FundIdKey = "FundId";

    public FundIdScopingMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Try route value first
        if (context.Request.RouteValues.TryGetValue("fundId", out var routeFundId)
            && Guid.TryParse(routeFundId?.ToString(), out var fundIdFromRoute))
        {
            context.Items[FundIdKey] = fundIdFromRoute;
        }
        // Fall back to header
        else if (context.Request.Headers.TryGetValue("X-Fund-Id", out var headerFundId)
            && Guid.TryParse(headerFundId.ToString(), out var fundIdFromHeader))
        {
            context.Items[FundIdKey] = fundIdFromHeader;
        }

        await _next(context);
    }
}

/// <summary>
/// Extension methods for accessing fund context from HttpContext.
/// </summary>
public static class FundContextExtensions
{
    public static Guid? GetFundId(this HttpContext context) =>
        context.Items.TryGetValue(FundIdScopingMiddleware.FundIdKey, out var value) && value is Guid fundId
            ? fundId
            : null;

    public static Guid GetRequiredFundId(this HttpContext context) =>
        context.GetFundId() ?? throw new InvalidOperationException("FundId not found in request context");

    public static Guid GetUserId(this HttpContext context)
    {
        var sub = context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
            ?? context.User.FindFirst("sub")?.Value;
        return sub is not null ? Guid.Parse(sub) : throw new InvalidOperationException("User ID not found in claims");
    }
}
