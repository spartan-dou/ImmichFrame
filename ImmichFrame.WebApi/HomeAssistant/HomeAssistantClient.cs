using System.Net.WebSockets;
using System.Text;
using System.Text.Json.Nodes;

namespace ImmichFrame.WebApi.HomeAssistant;

/// <summary>
/// Keeps one websocket open to Home Assistant and subscribes to the configured entities:
/// every state change is pushed, nothing is polled. Reconnects with a backoff.
/// </summary>
public class HomeAssistantClient : BackgroundService
{
    private static readonly TimeSpan MinDelay = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan MaxDelay = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan PingInterval = TimeSpan.FromSeconds(30);
    // Three missed pings: the connection is dead even if the socket has not noticed.
    private static readonly TimeSpan ReceiveTimeout = TimeSpan.FromSeconds(95);

    private readonly HomeAssistantConfig _config;
    private readonly HomeAssistantStateStore _store;
    private readonly ILogger<HomeAssistantClient> _logger;

    public HomeAssistantClient(HomeAssistantConfig config, HomeAssistantStateStore store, ILogger<HomeAssistantClient> logger)
    {
        _config = config;
        _store = store;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // An empty entity_ids would subscribe to every entity of Home Assistant.
        if (!_config.Enabled || _config.Sensors.Count == 0)
        {
            _logger.LogInformation("No Home Assistant sensor configured: not connecting");
            return;
        }

        var delay = MinDelay;
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (await RunSession(stoppingToken))
                {
                    delay = MinDelay;
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Home Assistant connection lost ({error}), retrying in {delay}s", ex.Message, delay.TotalSeconds);
            }
            finally
            {
                _store.SetConnected(false);
            }

            try
            {
                await Task.Delay(delay, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            delay = TimeSpan.FromSeconds(Math.Min(delay.TotalSeconds * 2, MaxDelay.TotalSeconds));
        }
    }

    /// <returns>Whether the subscription was established before the session ended.</returns>
    private async Task<bool> RunSession(CancellationToken stoppingToken)
    {
        using var socket = new ClientWebSocket();
        using var sessionCts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
        var ct = sessionCts.Token;

        await socket.ConnectAsync(_config.WebSocketUri, ct);

        await Expect(socket, "auth_required", ct);
        await Send(socket, new JsonObject { ["type"] = "auth", ["access_token"] = _config.Token }, ct);
        await Expect(socket, "auth_ok", ct);

        var entityIds = new JsonArray();
        foreach (var sensor in _config.Sensors)
        {
            entityIds.Add(sensor.Entity);
        }

        await Send(socket, new JsonObject { ["id"] = 1, ["type"] = "subscribe_entities", ["entity_ids"] = entityIds }, ct);

        var subscribed = false;
        Task? pingLoop = null;
        try
        {
            while (true)
            {
                using var receiveCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                receiveCts.CancelAfter(ReceiveTimeout);

                var message = await Receive(socket, receiveCts.Token);
                if (message == null)
                {
                    _logger.LogWarning("Home Assistant closed the connection");
                    return subscribed;
                }

                switch (message["type"]?.GetValue<string>())
                {
                    case "result" when message["id"]?.GetValue<int>() == 1:
                        if (message["success"]?.GetValue<bool>() != true)
                        {
                            throw new InvalidOperationException($"subscription refused: {message["error"]?.ToJsonString()}");
                        }

                        subscribed = true;
                        _store.SetConnected(true);
                        _logger.LogInformation("Subscribed to {count} Home Assistant entities", entityIds.Count);
                        pingLoop = PingLoop(socket, ct);
                        break;

                    case "event" when message["event"] is JsonObject entityEvent:
                        _store.Apply(entityEvent);
                        break;
                }
            }
        }
        finally
        {
            sessionCts.Cancel();
            if (pingLoop != null)
            {
                // Cleanup only: whatever ended the session has already been reported.
                try { await pingLoop; } catch (Exception) { }
            }
        }
    }

    private static async Task PingLoop(ClientWebSocket socket, CancellationToken ct)
    {
        // Id 1 is the subscription; pings take the following ones.
        var id = 1;
        while (!ct.IsCancellationRequested)
        {
            await Task.Delay(PingInterval, ct);
            await Send(socket, new JsonObject { ["id"] = ++id, ["type"] = "ping" }, ct);
        }
    }

    private static async Task Expect(ClientWebSocket socket, string type, CancellationToken ct)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(ReceiveTimeout);

        var message = await Receive(socket, timeout.Token)
            ?? throw new WebSocketException("connection closed during authentication");
        var actual = message["type"]?.GetValue<string>();
        if (actual != type)
        {
            // auth_invalid lands here: a wrong token is worth a clear log line.
            throw new InvalidOperationException($"expected '{type}', got '{actual}' {message["message"]?.GetValue<string>()}");
        }
    }

    private static async Task<JsonObject?> Receive(ClientWebSocket socket, CancellationToken ct)
    {
        var buffer = new byte[16 * 1024];
        using var message = new MemoryStream();
        while (true)
        {
            var result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), ct);
            if (result.MessageType == WebSocketMessageType.Close)
            {
                return null;
            }

            message.Write(buffer, 0, result.Count);
            if (result.EndOfMessage)
            {
                break;
            }
        }

        return JsonNode.Parse(message.ToArray()) as JsonObject;
    }

    private static Task Send(ClientWebSocket socket, JsonObject message, CancellationToken ct)
        => socket.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(message.ToJsonString())), WebSocketMessageType.Text, true, ct);
}
