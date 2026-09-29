using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ImmichFrame.WebApi.HomeAssistant;

public static class HomeAssistantServiceCollectionExtensions
{
    /// <summary>Overlay, notification and Home Assistant services of the fork, in one call from Program.cs.</summary>
    public static IServiceCollection AddHomeAssistant(this IServiceCollection services, string configPath)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<NotificationStore>();
        services.AddSingleton<HomeAssistantStateStore>();
        services.AddSingleton(srv => HomeAssistantConfig.Load(configPath,
            srv.GetRequiredService<ILoggerFactory>().CreateLogger<HomeAssistantConfig>()));
        services.AddHostedService<HomeAssistantClient>();

        return services;
    }
}
