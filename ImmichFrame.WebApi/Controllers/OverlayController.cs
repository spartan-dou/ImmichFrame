using ImmichFrame.Core.Interfaces;
using ImmichFrame.WebApi.HomeAssistant;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ImmichFrame.WebApi.Controllers
{
    public class OverlaySensorDto
    {
        public string Icon { get; set; } = string.Empty;
        /// <summary>Null when unknown: entity missing or unavailable, or no push for a while.</summary>
        public string? Value { get; set; }
        public string Unit { get; set; } = string.Empty;
    }

    public class OverlaySensorsDto
    {
        public List<OverlaySensorDto> Sensors { get; set; } = new();
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
        public List<OverlaySensorDto> Sensors { get; set; } = new();
        public OverlayNotificationDto? Notification { get; set; }
        public bool MemoriesEnabled { get; set; }
    }

    /// <summary>
    /// Everything the overlay draws on top of the slideshow, in one small poll; the
    /// values under the clock are pushed here by the Home Assistant integration.
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class OverlayController : ControllerBase
    {
        private readonly SensorStore _sensors;
        private readonly NotificationStore _notifications;
        private readonly IMemoriesSwitch _memoriesSwitch;

        public OverlayController(SensorStore sensors, NotificationStore notifications, IMemoriesSwitch memoriesSwitch)
        {
            _sensors = sensors;
            _notifications = notifications;
            _memoriesSwitch = memoriesSwitch;
        }

        [HttpGet(Name = "GetOverlay")]
        public OverlayDto Get()
        {
            var notification = _notifications.Current;
            return new OverlayDto
            {
                Sensors = _sensors.Current
                    .Select(s => new OverlaySensorDto { Icon = s.Icon, Value = s.Value, Unit = s.Unit })
                    .ToList(),
                Notification = notification == null ? null : new OverlayNotificationDto
                {
                    Message = notification.Message,
                    Link = notification.Link,
                    Until = notification.Until?.ToUnixTimeMilliseconds()
                },
                MemoriesEnabled = _memoriesSwitch.Enabled
            };
        }

        [HttpPut("Sensors", Name = "SetOverlaySensors")]
        public IActionResult SetSensors([FromBody] OverlaySensorsDto body)
        {
            _sensors.Set(body.Sensors.Select(s => new SensorValue(s.Icon ?? string.Empty, s.Value, s.Unit ?? string.Empty)));
            return NoContent();
        }
    }
}
