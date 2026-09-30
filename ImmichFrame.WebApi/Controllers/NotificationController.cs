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

        /// <summary>Minutes before the notification hides itself; empty or 0 keeps it until newer ones push it out.</summary>
        [JsonConverter(typeof(LenientNullableDoubleConverter))]
        public double? Duration { get; set; }
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
        public IActionResult Send([FromBody] NotificationRequestDto request)
        {
            _store.Add(request.Message, request.Link, request.Duration);
            return NoContent();
        }

        [HttpDelete(Name = "ClearNotification")]
        public IActionResult Clear()
        {
            _store.Clear();
            return NoContent();
        }
    }
}
