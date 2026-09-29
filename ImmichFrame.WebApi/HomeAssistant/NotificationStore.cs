namespace ImmichFrame.WebApi.HomeAssistant;

public record Notification(string Message, string Link, DateTimeOffset? Until);

/// <summary>
/// The one notification shown on top of the slideshow. Lives in memory: a restart clears it.
/// </summary>
public class NotificationStore
{
    public const int MaxLength = 1000;

    private readonly TimeProvider _time;
    private Notification? _current;

    public NotificationStore(TimeProvider time)
    {
        _time = time;
    }

    /// <summary>The notification to show, or null once it has expired.</summary>
    public Notification? Current
    {
        get
        {
            var current = _current;
            return current != null && (current.Until == null || current.Until > _time.GetUtcNow()) ? current : null;
        }
    }

    /// <param name="replace">False: ignored while another notification is still shown.</param>
    /// <returns>Whether the message is now the one shown. An empty message always clears.</returns>
    public bool Set(string? message, string? link, double? durationMinutes, bool replace)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            _current = null;
            return true;
        }

        if (!replace && Current != null)
        {
            return false;
        }

        var until = durationMinutes is double minutes && minutes > 0 ? _time.GetUtcNow().AddMinutes(minutes) : (DateTimeOffset?)null;
        var text = message.Length > MaxLength ? message[..MaxLength] : message;
        _current = new Notification(text, link?.Trim() ?? string.Empty, until);
        return true;
    }

    public void Clear() => _current = null;
}
