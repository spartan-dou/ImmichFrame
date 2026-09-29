using System.Text.Json.Nodes;
using ImmichFrame.Core.Interfaces;
using ImmichFrame.WebApi.HomeAssistant;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ImmichFrame.WebApi.Controllers
{
    public class OverlaySensorDto
    {
        public string Icon { get; set; } = string.Empty;
        /// <summary>Null when unknown: Home Assistant unreachable, entity missing or unavailable.</summary>
        public string? Value { get; set; }
        public string Unit { get; set; } = string.Empty;
    }

    public class OverlayNotificationDto
    {
        public string Message { get; set; } = string.Empty;
        public string Link { get; set; } = string.Empty;
        /// <summary>Unix milliseconds, or null for no end.</summary>
        public long? Until { get; set; }
    }

    public class OverlayDto
    {
        public bool Connected { get; set; }
        public List<OverlaySensorDto> Sensors { get; set; } = new();
        public OverlayNotificationDto? Notification { get; set; }
        public bool MemoriesEnabled { get; set; }
    }

    /// <summary>Everything the overlay draws on top of the slideshow, in one small poll.</summary>
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class OverlayController : ControllerBase
    {
        private static readonly string[] Unknown = { "unknown", "unavailable" };

        private readonly HomeAssistantConfig _config;
        private readonly HomeAssistantStateStore _states;
        private readonly NotificationStore _notifications;
        private readonly IMemoriesSwitch _memoriesSwitch;

        public OverlayController(HomeAssistantConfig config, HomeAssistantStateStore states,
            NotificationStore notifications, IMemoriesSwitch memoriesSwitch)
        {
            _config = config;
            _states = states;
            _notifications = notifications;
            _memoriesSwitch = memoriesSwitch;
        }

        [HttpGet(Name = "GetOverlay")]
        public OverlayDto Get()
        {
            var notification = _notifications.Current;
            return new OverlayDto
            {
                Connected = _states.Connected,
                Sensors = _config.Sensors.Select(ToDto).ToList(),
                Notification = notification == null ? null : new OverlayNotificationDto
                {
                    Message = notification.Message,
                    Link = notification.Link,
                    Until = notification.Until?.ToUnixTimeMilliseconds()
                },
                MemoriesEnabled = _memoriesSwitch.Enabled
            };
        }

        private OverlaySensorDto ToDto(HomeAssistantSensorConfig sensor)
        {
            var state = _states.Get(sensor.Entity);
            var isClimate = sensor.Domain == "climate";
            var attribute = sensor.Attribute ?? (isClimate ? "current_temperature" : null);

            string? value = null;
            if (state != null)
            {
                value = attribute == null ? state.State : Text(state.Attributes[attribute]);
            }

            if (value == null || Unknown.Contains(value))
            {
                value = null;
            }

            return new OverlaySensorDto
            {
                Icon = sensor.Icon ?? string.Empty,
                Value = value,
                Unit = sensor.Unit
                    ?? Text(state?.Attributes["unit_of_measurement"])
                    ?? (isClimate ? "°C" : string.Empty)
            };
        }

        private static string? Text(JsonNode? node) => node switch
        {
            null => null,
            JsonValue value when value.TryGetValue<string>(out var text) => text,
            _ => node.ToJsonString()
        };
    }
}
