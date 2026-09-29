namespace ImmichFrame.WebApi.HomeAssistant;

public record SensorValue(string Icon, string? Value, string Unit);

/// <summary>
/// Values pushed by the Home Assistant integration, shown under the clock. The
/// integration pushes on every change and every minute: past <see cref="StaleAfter"/>
/// without news, values are blanked rather than shown frozen.
/// </summary>
public class SensorStore
{
    public static readonly TimeSpan StaleAfter = TimeSpan.FromMinutes(5);

    private readonly TimeProvider _time;
    private IReadOnlyList<SensorValue> _sensors = Array.Empty<SensorValue>();
    private DateTimeOffset _updatedAt;

    public SensorStore(TimeProvider time)
    {
        _time = time;
    }

    public void Set(IEnumerable<SensorValue> sensors)
    {
        _sensors = sensors.ToList();
        _updatedAt = _time.GetUtcNow();
    }

    /// <summary>The values in the pushed order; each value is null once stale.</summary>
    public IReadOnlyList<SensorValue> Current
    {
        get
        {
            var sensors = _sensors;
            return _time.GetUtcNow() - _updatedAt < StaleAfter
                ? sensors
                : sensors.Select(sensor => sensor with { Value = null }).ToList();
        }
    }
}
