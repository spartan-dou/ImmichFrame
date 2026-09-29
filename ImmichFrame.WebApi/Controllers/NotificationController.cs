using System.Text.Json.Serialization;
using ImmichFrame.WebApi.HomeAssistant;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ImmichFrame.WebApi.Controllers
{
    public class NotificationRequestDto
    {
        public string? Message { get; set; }

        /// <summary>"/path" opens that Home Assistant view; http(s):// and homeassistant:// are opened as is.</summary>
        public string? Link { get; set; }

        /// <summary>Minutes before the notification hides itself; empty or 0 keeps it until the next one.</summary>
        [JsonConverter(typeof(LenientNullableDoubleConverter))]
        public double? Duration { get; set; }

        /// <summary>False: ignored while another notification is still shown. Defaults to true.</summary>
        [JsonConverter(typeof(LenientNullableBoolConverter))]
        public bool? Replace { get; set; }
    }

    public class NotificationResultDto
    {
        public bool Shown { get; set; }
    }

    /// <summary>Written by Home Assistant (REST notify), read by the slideshow through the overlay.</summary>
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class NotificationController : ControllerBase
    {
        private readonly NotificationStore _store;

        public NotificationController(NotificationStore store)
        {
            _store = store;
        }

        [HttpPost(Name = "SendNotification")]
        public NotificationResultDto Send([FromBody] NotificationRequestDto request)
            => new() { Shown = _store.Set(request.Message, request.Link, request.Duration, request.Replace ?? true) };

        [HttpDelete(Name = "ClearNotification")]
        public IActionResult Clear()
        {
            _store.Clear();
            return NoContent();
        }
    }
}
