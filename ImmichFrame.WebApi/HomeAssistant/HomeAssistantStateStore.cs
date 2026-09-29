using System.Text.Json.Nodes;

namespace ImmichFrame.WebApi.HomeAssistant;

public record EntityState(string? State, JsonObject Attributes);

/// <summary>
/// Latest known state of the followed entities, fed by the <c>subscribe_entities</c>
/// events of the Home Assistant websocket API. Published states are never mutated:
/// each change swaps in a fresh copy, so readers need no lock.
/// </summary>
public class HomeAssistantStateStore
{
    private readonly object _lock = new();
    private readonly Dictionary<string, EntityState> _states = new();

    public bool Connected { get; private set; }

    public void SetConnected(bool connected)
    {
        lock (_lock)
        {
            Connected = connected;
            if (!connected)
            {
                // Stale values would look live: show nothing until the next subscription.
                _states.Clear();
            }
        }
    }

    public EntityState? Get(string entityId)
    {
        lock (_lock)
        {
            return _states.GetValueOrDefault(entityId);
        }
    }

    /// <summary>Applies one <c>event</c> payload: <c>a</c> (full states), <c>c</c> (diffs), <c>r</c> (removals).</summary>
    public void Apply(JsonObject entityEvent)
    {
        lock (_lock)
        {
            if (entityEvent["a"] is JsonObject added)
            {
                foreach (var (entityId, node) in added)
                {
                    if (node is JsonObject state)
                    {
                        _states[entityId] = new EntityState(ReadState(state["s"]), CloneObject(state["a"]));
                    }
                }
            }

            if (entityEvent["c"] is JsonObject changed)
            {
                foreach (var (entityId, node) in changed)
                {
                    if (node is not JsonObject diff || !_states.TryGetValue(entityId, out var current))
                    {
                        continue;
                    }

                    var state = current.State;
                    var attributes = CloneObject(current.Attributes);

                    if (diff["+"] is JsonObject additions)
                    {
                        if (additions.ContainsKey("s"))
                        {
                            state = ReadState(additions["s"]);
                        }

                        if (additions["a"] is JsonObject changedAttributes)
                        {
                            foreach (var (key, value) in changedAttributes)
                            {
                                attributes[key] = value?.DeepClone();
                            }
                        }
                    }

                    if (diff["-"] is JsonObject removals && removals["a"] is JsonArray removedAttributes)
                    {
                        foreach (var key in removedAttributes)
                        {
                            if (key is JsonValue value && value.TryGetValue<string>(out var name))
                            {
                                attributes.Remove(name);
                            }
                        }
                    }

                    _states[entityId] = new EntityState(state, attributes);
                }
            }

            if (entityEvent["r"] is JsonArray removed)
            {
                foreach (var node in removed)
                {
                    if (node is JsonValue value && value.TryGetValue<string>(out var entityId))
                    {
                        _states.Remove(entityId);
                    }
                }
            }
        }
    }

    private static string? ReadState(JsonNode? node) => node switch
    {
        null => null,
        JsonValue value when value.TryGetValue<string>(out var text) => text,
        _ => node.ToJsonString()
    };

    private static JsonObject CloneObject(JsonNode? node)
        => node is JsonObject obj ? obj.DeepClone().AsObject() : new JsonObject();
}
