using System.Text.Json;
using ImmichFrame.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace ImmichFrame.Core.Logic;

/// <summary>
/// Memories switch persisted to <paramref name="stateFile"/>, so a restart keeps what the
/// API last set. Until the API says otherwise memories are hidden, as ShowMemories
/// defaults to upstream: no memories request is made before they are asked for.
/// </summary>
public class MemoriesSwitch : IMemoriesSwitch
{
    private record State(bool Enabled);

    private readonly object _lock = new();
    private readonly string? _stateFile;
    private readonly ILogger<MemoriesSwitch>? _logger;
    private bool _enabled;

    public MemoriesSwitch(string? stateFile = null, ILogger<MemoriesSwitch>? logger = null)
    {
        _stateFile = stateFile;
        _logger = logger;

        if (_stateFile == null || !File.Exists(_stateFile))
        {
            return;
        }

        try
        {
            _enabled = JsonSerializer.Deserialize<State>(File.ReadAllText(_stateFile))?.Enabled ?? false;
        }
        catch (Exception ex)
        {
            // Unreadable state: start rather than fail, with the default.
            _logger?.LogError(ex, "Cannot read {file}: memories hidden until the API sets them again", _stateFile);
        }
    }

    public bool Enabled
    {
        get
        {
            lock (_lock)
            {
                return _enabled;
            }
        }
        set
        {
            lock (_lock)
            {
                _enabled = value;
                Save();
            }
        }
    }

    private void Save()
    {
        if (_stateFile == null)
        {
            return;
        }

        var directory = Path.GetDirectoryName(_stateFile);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        // Written aside then moved: a crash mid-write never leaves a truncated file.
        var temporary = _stateFile + ".tmp";
        File.WriteAllText(temporary, JsonSerializer.Serialize(new State(_enabled)));
        File.Move(temporary, _stateFile, overwrite: true);
    }
}
