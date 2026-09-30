namespace ImmichFrame.WebApi.HomeAssistant;

public record Notification(string Message, string Link, DateTimeOffset? Until);

/// <summary>
/// The notifications shown on top of the slideshow, newest first. Lives in memory: a
/// restart clears them.
/// </summary>
public class NotificationStore
{
    public const int MaxLength = 1000;
    public const int MaxShown = 3;

    private readonly TimeProvider _time;
    private readonly object _gate = new();
    private List<Notification> _items = new();

    public NotificationStore(TimeProvider time)
    {
        _time = time;
    }

    /// <summary>The notifications still shown, newest first.</summary>
    public IReadOnlyList<Notification> Current
    {
        get
        {
            lock (_gate)
            {
                return Shown().ToList();
            }
        }
    }

    /// <summary>
    /// Puts the message on top; past <see cref="MaxShown"/>, the oldest goes. A message
    /// already shown moves up rather than taking a second slot. An empty message clears them all.
    /// </summary>
    public void Add(string? message, string? link, double? durationMinutes)
    {
        lock (_gate)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                _items = new();
                return;
            }

            var until = durationMinutes is double minutes && minutes > 0 ? _time.GetUtcNow().AddMinutes(minutes) : (DateTimeOffset?)null;
            var text = message.Length > MaxLength ? message[..MaxLength] : message;
            _items = Shown()
                .Where(n => n.Message != text)
                .Prepend(new Notification(text, link?.Trim() ?? string.Empty, until))
                .Take(MaxShown)
                .ToList();
        }
    }

    public void Clear()
    {
        lock (_gate)
        {
            _items = new();
        }
    }

    private IEnumerable<Notification> Shown()
    {
        var now = _time.GetUtcNow();
        return _items.Where(n => n.Until == null || n.Until > now);
    }
}
