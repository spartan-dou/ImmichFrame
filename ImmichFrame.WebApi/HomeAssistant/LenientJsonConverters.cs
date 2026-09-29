using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ImmichFrame.WebApi.HomeAssistant;

// Home Assistant's REST notify renders every extra field as text ("30", "True", ""):
// these accept the text form as well as the JSON one.

public class LenientNullableDoubleConverter : JsonConverter<double?>
{
    public override double? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        switch (reader.TokenType)
        {
            case JsonTokenType.Number:
                return reader.GetDouble();
            case JsonTokenType.String:
                var text = reader.GetString()?.Trim();
                if (string.IsNullOrEmpty(text))
                {
                    return null;
                }

                return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
                    ? value
                    : throw new JsonException($"'{text}' is not a number");
            default:
                throw new JsonException($"Unexpected {reader.TokenType} for a number");
        }
    }

    public override void Write(Utf8JsonWriter writer, double? value, JsonSerializerOptions options)
    {
        if (value.HasValue) writer.WriteNumberValue(value.Value);
        else writer.WriteNullValue();
    }
}

public class LenientNullableBoolConverter : JsonConverter<bool?>
{
    public override bool? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        switch (reader.TokenType)
        {
            case JsonTokenType.True:
                return true;
            case JsonTokenType.False:
                return false;
            case JsonTokenType.String:
                var text = reader.GetString()?.Trim();
                if (string.IsNullOrEmpty(text))
                {
                    return null;
                }

                if (bool.TryParse(text, out var value))
                {
                    return value;
                }

                return text switch
                {
                    "1" or "on" or "yes" => true,
                    "0" or "off" or "no" => false,
                    _ => throw new JsonException($"'{text}' is not a boolean")
                };
            default:
                throw new JsonException($"Unexpected {reader.TokenType} for a boolean");
        }
    }

    public override void Write(Utf8JsonWriter writer, bool? value, JsonSerializerOptions options)
    {
        if (value.HasValue) writer.WriteBooleanValue(value.Value);
        else writer.WriteNullValue();
    }
}
