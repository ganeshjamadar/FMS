using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FundManager.BuildingBlocks.Middleware;

/// <summary>
/// Idempotency middleware that caches responses for requests with Idempotency-Key header.
/// Used for payment recording and other financial mutations.
/// Per research.md Section 7 and FR-114.
/// </summary>
public class IdempotencyMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<IdempotencyMiddleware> _logger;

    public IdempotencyMiddleware(RequestDelegate next, ILogger<IdempotencyMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, IIdempotencyStore store)
    {
        // Only process POST/PUT/PATCH requests with Idempotency-Key header
        if (!IsIdempotentRequest(context))
        {
            await _next(context);
            return;
        }

        var idempotencyKey = context.Request.Headers["Idempotency-Key"].ToString();
        var fundId = context.GetFundId();
        var endpoint = $"{context.Request.Method} {context.Request.Path}";

        // Check for existing record
        var existing = await store.GetAsync(fundId ?? Guid.Empty, idempotencyKey, endpoint);
        if (existing is not null)
        {
            _logger.LogInformation("Idempotent request replayed: Key={Key}, Endpoint={Endpoint}", idempotencyKey, endpoint);
            context.Response.StatusCode = existing.StatusCode;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(existing.ResponseBody);
            return;
        }

        // Capture response
        var originalBody = context.Response.Body;
        using var memoryStream = new MemoryStream();
        context.Response.Body = memoryStream;

        await _next(context);

        memoryStream.Seek(0, SeekOrigin.Begin);
        var responseBody = await new StreamReader(memoryStream).ReadToEndAsync();

        // Store the response for successful requests
        if (context.Response.StatusCode >= 200 && context.Response.StatusCode < 300)
        {
            await store.SaveAsync(new IdempotencyRecord
            {
                Id = Guid.NewGuid(),
                IdempotencyKey = idempotencyKey,
                FundId = fundId ?? Guid.Empty,
                Endpoint = endpoint,
                StatusCode = context.Response.StatusCode,
                ResponseBody = responseBody,
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddDays(90)
            });
        }

        // Copy response back to original stream
        memoryStream.Seek(0, SeekOrigin.Begin);
        await memoryStream.CopyToAsync(originalBody);
        context.Response.Body = originalBody;
    }

    private static bool IsIdempotentRequest(HttpContext context) =>
        context.Request.Headers.ContainsKey("Idempotency-Key")
        && (context.Request.Method == HttpMethods.Post
            || context.Request.Method == HttpMethods.Put
            || context.Request.Method == HttpMethods.Patch);
}

/// <summary>
/// Record stored for idempotent request deduplication.
/// </summary>
public class IdempotencyRecord
{
    public Guid Id { get; set; }
    public string IdempotencyKey { get; set; } = string.Empty;
    public Guid FundId { get; set; }
    public string Endpoint { get; set; } = string.Empty;
    public int StatusCode { get; set; }
    public string ResponseBody { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
}

/// <summary>
/// Persistence interface for idempotency records.
/// Implemented by each service's infrastructure layer.
/// </summary>
public interface IIdempotencyStore
{
    Task<IdempotencyRecord?> GetAsync(Guid fundId, string idempotencyKey, string endpoint);
    Task SaveAsync(IdempotencyRecord record);
}
