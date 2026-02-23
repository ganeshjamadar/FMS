using FluentValidation;
using FluentValidation.AspNetCore;
using FundManager.FundAdmin.Infrastructure.Data;
using FundManager.FundAdmin.Domain.Services;
using FundManager.BuildingBlocks.Auth;
using FundManager.BuildingBlocks.Audit;
using FundManager.BuildingBlocks.Messaging;
using FundManager.ServiceDefaults;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;

var builder = WebApplication.CreateBuilder(args);

// Serilog + OpenTelemetry
builder.Host.AddServiceDefaults("fund-manager-fund-admin");
builder.Services.AddServiceDefaults("fund-manager-fund-admin");

// EF Core + PostgreSQL
builder.Services.AddDbContext<FundAdminDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// MassTransit + RabbitMQ
builder.Services.AddMassTransitWithRabbitMq(builder.Configuration);

// Domain services
builder.Services.AddScoped<FundService>();
builder.Services.AddScoped<InvitationService>();
builder.Services.AddScoped<MemberService>();
builder.Services.AddSingleton<JwtTokenService>();
builder.Services.AddScoped<AuditEventPublisher>();

// Redis distributed cache
builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = builder.Configuration.GetConnectionString("Redis") ?? "localhost:6379";
    options.InstanceName = "fundmanager:fundadmin:";
});

// Controllers + FluentValidation
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddProblemDetails();
builder.Services.AddFluentValidationAutoValidation();
builder.Services.AddValidatorsFromAssemblyContaining<Program>();

// JWT Auth
var jwtService = new JwtTokenService(builder.Configuration);
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
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

var app = builder.Build();

// Auto-create tables from DbContext model (CreateTables instead of EnsureCreated
// because all services share one database; EnsureCreated skips if any tables exist)
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<FundAdminDbContext>();
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
