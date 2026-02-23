using System.Text;
using System.Threading.RateLimiting;
using FundManager.ApiGateway.Middleware;
using FundManager.ApiGateway.Reports;
using FundManager.ApiGateway.Services;
using FundManager.BuildingBlocks.Auth;
using FundManager.ServiceDefaults;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

// Serilog + OpenTelemetry
builder.Host.AddServiceDefaults("fund-manager-gateway");
builder.Services.AddServiceDefaults("fund-manager-gateway");

// YARP Reverse Proxy
builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

// Controllers (for ReportsController)
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddProblemDetails();

// JWT Auth
var jwtService = new JwtTokenService(builder.Configuration);
builder.Services.AddSingleton(jwtService);

builder.Services.AddAuthentication(o =>
{
    o.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    o.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(o =>
{
    o.TokenValidationParameters = jwtService.GetValidationParameters();
});
builder.Services.AddAuthorization(options =>
{
    FundAuthorizationPolicies.ConfigurePolicies(options);
});

// HttpClients for downstream services
builder.Services.AddHttpClient("identity", c =>
    c.BaseAddress = new Uri(builder.Configuration["ServiceUrls:Identity"] ?? "http://localhost:5001"));
builder.Services.AddHttpClient("fundadmin", c =>
    c.BaseAddress = new Uri(builder.Configuration["ServiceUrls:FundAdmin"] ?? "http://localhost:5002"));
builder.Services.AddHttpClient("contributions", c =>
    c.BaseAddress = new Uri(builder.Configuration["ServiceUrls:Contributions"] ?? "http://localhost:5003"));
builder.Services.AddHttpClient("loans", c =>
    c.BaseAddress = new Uri(builder.Configuration["ServiceUrls:Loans"] ?? "http://localhost:5004"));
builder.Services.AddHttpClient("dissolution", c =>
    c.BaseAddress = new Uri(builder.Configuration["ServiceUrls:Dissolution"] ?? "http://localhost:5005"));
builder.Services.AddHttpClient("notifications", c =>
    c.BaseAddress = new Uri(builder.Configuration["ServiceUrls:Notifications"] ?? "http://localhost:5006"));
builder.Services.AddHttpClient("audit", c =>
    c.BaseAddress = new Uri(builder.Configuration["ServiceUrls:Audit"] ?? "http://localhost:5007"));

// Report services
builder.Services.AddScoped<ReportAggregationService>();
builder.Services.AddScoped<CsvReportGenerator>();
builder.Services.AddScoped<PdfReportGenerator>();

// Redis distributed cache
builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = builder.Configuration.GetConnectionString("Redis") ?? "localhost:6379";
    options.InstanceName = "fundmanager:gateway:";
});

// Rate Limiting (NFR-006)
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    // General: 100 requests per minute per IP
    options.AddFixedWindowLimiter("general", opt =>
    {
        opt.PermitLimit = 100;
        opt.Window = TimeSpan.FromMinutes(1);
        opt.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        opt.QueueLimit = 0;
    });

    // OTP: 5 requests per 15 minutes per IP
    options.AddFixedWindowLimiter("otp", opt =>
    {
        opt.PermitLimit = 5;
        opt.Window = TimeSpan.FromMinutes(15);
        opt.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        opt.QueueLimit = 0;
    });

    // Default: apply general limiter by IP
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 100,
                Window = TimeSpan.FromMinutes(1)
            }));
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseExceptionHandler();
app.UseStatusCodePages();

app.UseRateLimiter();
app.UseAuthentication();
app.UseMiddleware<FundRoleEnrichmentMiddleware>();
app.UseAuthorization();

app.MapControllers();
app.MapHealthChecks("/health");
app.MapReverseProxy();

app.Run();
