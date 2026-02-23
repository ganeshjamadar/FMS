using FundManager.Loans.Infrastructure.Data;
using FundManager.Loans.Domain.Services;
using FundManager.Loans.Infrastructure.Jobs;
using FundManager.BuildingBlocks.Audit;
using FundManager.BuildingBlocks.Auth;
using FundManager.BuildingBlocks.Messaging;
using FundManager.ServiceDefaults;
using FluentValidation;
using FluentValidation.AspNetCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.IdentityModel.Tokens;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// Serilog + OpenTelemetry
builder.Host.AddServiceDefaults("fund-manager-loans");
builder.Services.AddServiceDefaults("fund-manager-loans");

// EF Core + PostgreSQL
builder.Services.AddDbContext<LoansDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// MassTransit + RabbitMQ
builder.Services.AddMassTransitWithRabbitMq(builder.Configuration);

// Domain services
builder.Services.AddScoped<LoanRequestService>();
builder.Services.AddScoped<RepaymentCalculationService>();
builder.Services.AddScoped<RepaymentRecordingService>();
builder.Services.AddScoped<VotingService>();
builder.Services.AddScoped<PenaltyService>();
builder.Services.AddScoped<AuditEventPublisher>();

// Background jobs
builder.Services.AddHostedService<RepaymentOverdueJob>();

// Controllers + FluentValidation
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddProblemDetails();
builder.Services.AddFluentValidationAutoValidation();
builder.Services.AddValidatorsFromAssemblyContaining<Program>();

// JWT Auth
builder.Services.AddSingleton<JwtTokenService>();
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        var jwtKey = builder.Configuration["Jwt:Key"] ?? "super-secret-dev-key-min-32-chars!!";
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = false,
            ValidateIssuerSigningKey = true,
            ValidIssuer = "FundManager",
            ValidAudience = "FundManager",
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
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
    var db = scope.ServiceProvider.GetRequiredService<LoansDbContext>();
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
