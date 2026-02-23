using MassTransit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using FundManager.BuildingBlocks.Audit;

namespace FundManager.BuildingBlocks.Messaging;

/// <summary>
/// MassTransit + RabbitMQ configuration shared by all services.
/// Per research.md Section 2.
/// </summary>
public static class MassTransitConfig
{
    /// <summary>
    /// Add MassTransit with RabbitMQ transport to the service collection.
    /// Each service passes its consumer configuration.
    /// </summary>
    public static IServiceCollection AddMassTransitWithRabbitMq(
        this IServiceCollection services,
        IConfiguration configuration,
        Action<IBusRegistrationConfigurator>? configureConsumers = null)
    {
        services.AddMassTransit(bus =>
        {
            bus.SetKebabCaseEndpointNameFormatter();

            configureConsumers?.Invoke(bus);

            bus.UsingRabbitMq((context, cfg) =>
            {
                var host = configuration["RabbitMQ:Host"] ?? "localhost";
                var port = ushort.TryParse(configuration["RabbitMQ:Port"], out var p) ? p : (ushort)5672;
                var username = configuration["RabbitMQ:Username"] ?? "guest";
                var password = configuration["RabbitMQ:Password"] ?? "guest";

                cfg.Host(host, port, "/", h =>
                {
                    h.Username(username);
                    h.Password(password);
                });

                cfg.ConfigureEndpoints(context);
            });
        });

        // Register AuditEventPublisher
        services.AddScoped<AuditEventPublisher>();

        return services;
    }
}
