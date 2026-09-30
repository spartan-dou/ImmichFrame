using ImmichFrame.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ImmichFrame.WebApi.Controllers
{
    public class MemoriesStateDto
    {
        public bool Enabled { get; set; }

        /// <summary>Memories alone, whatever <see cref="Enabled"/> says; the usual assets on a day without any.</summary>
        public bool Only { get; set; }
    }

    /// <summary>A field left out keeps its value.</summary>
    public class MemoriesUpdateDto
    {
        public bool? Enabled { get; set; }
        public bool? Only { get; set; }
    }

    /// <summary>
    /// Shows or hides memories at runtime, e.g. from a Home Assistant RESTful switch.
    /// The only source of truth for memories: kept across restarts, ShowMemories is ignored.
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class MemoriesController : ControllerBase
    {
        private readonly ILogger<MemoriesController> _logger;
        private readonly IMemoriesSwitch _memoriesSwitch;

        public MemoriesController(ILogger<MemoriesController> logger, IMemoriesSwitch memoriesSwitch)
        {
            _logger = logger;
            _memoriesSwitch = memoriesSwitch;
        }

        [HttpGet(Name = "GetMemories")]
        public MemoriesStateDto Get() => new() { Enabled = _memoriesSwitch.Enabled, Only = _memoriesSwitch.Only };

        [HttpPut(Name = "SetMemories")]
        public MemoriesStateDto Set([FromBody] MemoriesUpdateDto update)
        {
            if (update.Enabled is bool enabled)
            {
                _memoriesSwitch.Enabled = enabled;
                _logger.LogInformation("Memories {state}", enabled ? "shown" : "hidden");
            }

            if (update.Only is bool only)
            {
                _memoriesSwitch.Only = only;
                _logger.LogInformation("Memories only {state}", only ? "on" : "off");
            }

            return Get();
        }
    }
}
