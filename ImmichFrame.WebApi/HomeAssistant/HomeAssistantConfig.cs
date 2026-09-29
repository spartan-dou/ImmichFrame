using YamlDotNet.Serialization;

namespace ImmichFrame.WebApi.HomeAssistant;

/// <summary>
/// <c>HomeAssistant.yml</c>, next to the settings: which Home Assistant to follow and
/// which entities to show, in the spirit of ESPHome's <c>homeassistant</c> sensors.
/// Kept out of the settings database on purpose, so the admin UI round-trip never
/// drops it. No file, no Home Assistant: the overlay only shows the clock.
/// </summary>
public class HomeAssistantConfig
{
    public const string FileName = "HomeAssistant.yml";

    public string Url { get; set; } = string.Empty;
    public string? Token { get; set; }
    public string? TokenFile { get; set; }
    public List<HomeAssistantSensorConfig> Sensors { get; set; } = new();

    public bool Enabled => !string.IsNullOrWhiteSpace(Url) && !string.IsNullOrWhiteSpace(Token);

    public Uri WebSocketUri
    {
        get
        {
            var builder = new UriBuilder(Url.TrimEnd('/') + "/api/websocket");
            builder.Scheme = builder.Scheme == Uri.UriSchemeHttps ? "wss" : "ws";
            return builder.Uri;
        }
    }

    public static HomeAssistantConfig Load(string configPath, ILogger logger)
    {
        var path = Path.Combine(configPath, FileName);
        if (!File.Exists(path))
        {
            return new HomeAssistantConfig();
        }

        HomeAssistantConfig config;
        try
        {
            config = new DeserializerBuilder()
                .IgnoreUnmatchedProperties()
                .Build()
                .Deserialize<HomeAssistantConfig>(File.ReadAllText(path)) ?? new HomeAssistantConfig();

            if (!string.IsNullOrWhiteSpace(config.TokenFile))
            {
                config.Token = File.ReadAllText(config.TokenFile).Trim();
            }
        }
        catch (Exception ex)
        {
            // The slideshow matters more than the overlay: start without Home Assistant.
            logger.LogError(ex, "Cannot read {file}: Home Assistant stays disconnected", FileName);
            return new HomeAssistantConfig();
        }

        if (!config.Enabled)
        {
            logger.LogWarning("{file} found but Url or token is missing: Home Assistant stays disconnected", FileName);
        }

        return config;
    }
}

public class HomeAssistantSensorConfig
{
    public string Entity { get; set; } = string.Empty;
    public string? Icon { get; set; }

    /// <summary>Attribute to show instead of the state. Defaults to <c>current_temperature</c> for a climate entity.</summary>
    public string? Attribute { get; set; }

    /// <summary>Defaults to the entity's <c>unit_of_measurement</c>, then to °C for a climate entity.</summary>
    public string? Unit { get; set; }

    public string Domain => Entity.Split('.', 2)[0];
}
