using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ImmichFrame.WebApi.HomeAssistant;

public static class HomeAssistantServiceCollectionExtensions
{
    /// <summary>Stores fed by the Home Assistant integration, in one call from Program.cs.</summary>
    public static IServiceCollection AddHomeAssistant(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<NotificationStore>();
        services.AddSingleton<SensorStore>();

        return services;
    }
}
