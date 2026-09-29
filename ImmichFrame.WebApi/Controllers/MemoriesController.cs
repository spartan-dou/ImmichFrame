using ImmichFrame.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ImmichFrame.WebApi.Controllers
{
    public class MemoriesStateDto
    {
        public bool Enabled { get; set; }
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
        public MemoriesStateDto Get() => new() { Enabled = _memoriesSwitch.Enabled };

        [HttpPut(Name = "SetMemories")]
        public MemoriesStateDto Set([FromBody] MemoriesStateDto state)
        {
            _memoriesSwitch.Enabled = state.Enabled;
            _logger.LogInformation("Memories {state}", state.Enabled ? "shown" : "hidden");
            return new() { Enabled = _memoriesSwitch.Enabled };
        }
    }
}
