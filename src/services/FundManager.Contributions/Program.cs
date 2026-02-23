using FundManager.BuildingBlocks.Auth;
using FundManager.BuildingBlocks.Audit;
using FundManager.Contributions.Domain.Services;
using FundManager.Contributions.Infrastructure.Data;
using FundManager.Contributions.Infrastructure.Jobs;
using FundManager.BuildingBlocks.Messaging;
using FundManager.ServiceDefaults;
using FluentValidation;
using FluentValidation.AspNetCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;

var builder = WebApplication.CreateBuilder(args);

// Serilog + OpenTelemetry
builder.Host.AddServiceDefaults("fund-manager-contributions");
builder.Services.AddServiceDefaults("fund-manager-contributions");

// EF Core + PostgreSQL
builder.Services.AddDbContext<ContributionsDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// MassTransit + RabbitMQ
builder.Services.AddMassTransitWithRabbitMq(builder.Configuration);

// Domain services
builder.Services.AddScoped<ContributionCycleService>();
builder.Services.AddScoped<PaymentService>();
builder.Services.AddScoped<OverdueDetectionService>();

// Audit
builder.Services.AddScoped<AuditEventPublisher>();

// Background jobs
builder.Services.AddHostedService<OverdueDetectionJob>();

// JWT Auth
var jwtService = new JwtTokenService(builder.Configuration);
builder.Services.AddSingleton(jwtService);
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = jwtService.GetValidationParameters();
    });
builder.Services.AddHttpContextAccessor();
builder.Services.AddTransient<Microsoft.AspNetCore.Authentication.IClaimsTransformation, FundRoleClaimsTransformation>();
builder.Services.AddAuthorization(options =>
{
    FundAuthorizationPolicies.ConfigurePolicies(options);
});

// Controllers + FluentValidation
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddProblemDetails();
builder.Services.AddFluentValidationAutoValidation();
builder.Services.AddValidatorsFromAssemblyContaining<Program>();

var app = builder.Build();

// Auto-create tables from DbContext model (CreateTables instead of EnsureCreated
// because all services share one database; EnsureCreated skips if any tables exist)
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ContributionsDbContext>();
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
