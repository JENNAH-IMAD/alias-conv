using System.Text.Json;
using System.Text.Json.Serialization;

namespace BankStatementConverter.Infrastructure.Exports;

internal static class StatementJson
{
    public static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            NumberHandling = JsonNumberHandling.AllowReadingFromString
        };
        options.Converters.Add(new TextConverter());
        options.Converters.Add(new RawResultConverter());
        return options;
    }

    // PDF font mappings and OCR responses can contain U+0000. JSON permits it,
    // but PostgreSQL jsonb cannot store it. Preserve visible text and whitespace.
    private static string Clean(string value) => value.Replace("\0", "", StringComparison.Ordinal);

    private sealed class TextConverter : JsonConverter<string>
    {
        public override string? Read(ref Utf8JsonReader reader, Type type, JsonSerializerOptions options) => reader.GetString();
        public override string ReadAsPropertyName(ref Utf8JsonReader reader, Type type, JsonSerializerOptions options) => reader.GetString()!;
        public override void Write(Utf8JsonWriter writer, string value, JsonSerializerOptions options) => writer.WriteStringValue(Clean(value));
        public override void WriteAsPropertyName(Utf8JsonWriter writer, string value, JsonSerializerOptions options) => writer.WritePropertyName(Clean(value));
    }

    private sealed class RawResultConverter : JsonConverter<JsonElement>
    {
        public override JsonElement Read(ref Utf8JsonReader reader, Type type, JsonSerializerOptions options)
        {
            using var document = JsonDocument.ParseValue(ref reader);
            return document.RootElement.Clone();
        }

        public override void Write(Utf8JsonWriter writer, JsonElement value, JsonSerializerOptions options)
        {
            switch (value.ValueKind)
            {
                case JsonValueKind.Object:
                    writer.WriteStartObject();
                    foreach (var property in value.EnumerateObject())
                    {
                        writer.WritePropertyName(Clean(property.Name));
                        Write(writer, property.Value, options);
                    }
                    writer.WriteEndObject();
                    break;
                case JsonValueKind.Array:
                    writer.WriteStartArray();
                    foreach (var item in value.EnumerateArray())
                        Write(writer, item, options);
                    writer.WriteEndArray();
                    break;
                case JsonValueKind.String:
                    writer.WriteStringValue(Clean(value.GetString()!));
                    break;
                default:
                    value.WriteTo(writer);
                    break;
            }
        }
    }
}
