using FundManager.Dissolution.Infrastructure.Data;
using FundManager.Dissolution.Domain.Services;
using FundManager.BuildingBlocks.Messaging;
using FundManager.BuildingBlocks.Auth;
using FundManager.BuildingBlocks.Audit;
using FundManager.ServiceDefaults;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.IdentityModel.Tokens;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// Serilog + OpenTelemetry
builder.Host.AddServiceDefaults("fund-manager-dissolution");
builder.Services.AddServiceDefaults("fund-manager-dissolution");

// EF Core + PostgreSQL
builder.Services.AddDbContext<DissolutionDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// MassTransit + RabbitMQ
builder.Services.AddMassTransitWithRabbitMq(builder.Configuration);

// Domain services
builder.Services.AddScoped<DissolutionService>();
builder.Services.AddScoped<SettlementReportGenerator>();
builder.Services.AddScoped<AuditEventPublisher>();
builder.Services.AddSingleton<JwtTokenService>();

// Controllers + FluentValidation
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddProblemDetails();

// Auth
var jwtKey = builder.Configuration["Jwt:Key"] ?? "super-secret-dev-key-change-in-production-min-32-chars!!";
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"] ?? "FundManager",
            ValidAudience = builder.Configuration["Jwt:Audience"] ?? "FundManager",
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
        };
    });
builder.Services.AddHttpContextAccessor();
builder.Services.AddTransient<Microsoft.AspNetCore.Authentication.IClaimsTransformation, FundRoleClaimsTransformation>();
builder.Services.AddAuthorization(options =>
{
    FundAuthorizationPolicies.ConfigurePolicies(options);
});

var app = builder.Build();

// Auto-create tables from DbContext model (CreateTables instead of EnsureCreated
// because all services share one database; EnsureCreated skips if any tables exist)
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<DissolutionDbContext>();
    try
    {
        var creator = db.GetService<IRelationalDatabaseCreator>();
        creator.CreateTables();
    }
    catch (Npgsql.PostgresException)
    {
        // Tables already exist — safe to ignore on restart
    }
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseExceptionHandler();
app.UseStatusCodePages();

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHealthChecks("/health");

app.Run();
