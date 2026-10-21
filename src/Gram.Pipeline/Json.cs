using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Gram.Pipeline;

/// <summary>One JSON configuration for reading sources and writing app data (camelCase, compact, readable Unicode).</summary>
internal static class Json
{
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        // Keep Cyrillic, umlauts and „quotes“ readable in the output instead of \uXXXX escapes.
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        // Enums as names, the way the app's source-generated reader (DataJson) expects them.
        Converters = { new JsonStringEnumConverter() },
    };

    public static T Read<T>(string path) =>
        JsonSerializer.Deserialize<T>(File.ReadAllText(path), Options)
        ?? throw new InvalidDataException($"{path} is empty.");

    public static string Write<T>(T value) => JsonSerializer.Serialize(value, Options);
}
