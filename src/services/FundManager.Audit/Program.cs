using FundManager.Audit.Infrastructure.Consumers;
using FundManager.Audit.Infrastructure.Data;
using FundManager.BuildingBlocks.Auth;
using FundManager.BuildingBlocks.Messaging;
using FundManager.ServiceDefaults;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;

var builder = WebApplication.CreateBuilder(args);

// Serilog + OpenTelemetry
builder.Host.AddServiceDefaults("fund-manager-audit");
builder.Services.AddServiceDefaults("fund-manager-audit");

// EF Core + PostgreSQL
builder.Services.AddDbContext<AuditDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// MassTransit + RabbitMQ (with AuditLogConsumer)
builder.Services.AddMassTransitWithRabbitMq(builder.Configuration, bus =>
{
    bus.AddConsumer<AuditLogConsumer>();
});

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

// Controllers
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddProblemDetails();

var app = builder.Build();

// Auto-create tables from DbContext model (CreateTables instead of EnsureCreated
// because all services share one database; EnsureCreated skips if any tables exist)
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AuditDbContext>();
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
