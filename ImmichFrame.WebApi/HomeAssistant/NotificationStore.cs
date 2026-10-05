namespace ImmichFrame.WebApi.HomeAssistant;

/// <param name="Id">Unique while the server runs: the page keys its list on it.</param>
/// <param name="Tag">Empty when the sender gave none.</param>
public record Notification(long Id, string Message, string Link, DateTimeOffset? Until, string Tag);

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
    private long _lastId;

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
    /// Puts the message on top; past <see cref="MaxShown"/>, the oldest goes. It replaces the
    /// one with the same tag, as a phone notification does, or the same message rather than
    /// taking a second slot. An empty message clears the one with its tag, or all of them.
    /// </summary>
    public void Add(string? message, string? link, double? durationMinutes, string? tag = null)
    {
        var key = tag?.Trim() ?? string.Empty;
        lock (_gate)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                _items = key == string.Empty ? new() : Shown().Where(n => n.Tag != key).ToList();
                return;
            }

            var until = durationMinutes is double minutes && minutes > 0 ? _time.GetUtcNow().AddMinutes(minutes) : (DateTimeOffset?)null;
            var text = message.Length > MaxLength ? message[..MaxLength] : message;
            _items = Shown()
                .Where(n => n.Message != text && (key == string.Empty || n.Tag != key))
                .Prepend(new Notification(++_lastId, text, link?.Trim() ?? string.Empty, until, key))
                .Take(MaxShown)
                .ToList();
        }
    }

    /// <summary>Clears the notification with <paramref name="tag"/>, or all of them without one.</summary>
    public void Clear(string? tag = null) => Add(null, null, null, tag);

    private IEnumerable<Notification> Shown()
    {
        var now = _time.GetUtcNow();
        return _items.Where(n => n.Until == null || n.Until > now);
    }
}
