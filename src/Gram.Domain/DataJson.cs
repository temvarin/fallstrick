using System.Text.Json;
using System.Text.Json.Serialization;

namespace Gram.Domain;

/// <summary>The catalog as one JSON file (wwwroot/data/catalog.json).</summary>
public sealed record CatalogData(
    IReadOnlyList<Noun> Nouns, IReadOnlyList<RektionEntry> Rektion, IReadOnlyList<CaseFrame> CaseFrames,
    IReadOnlyList<EndingFrame> EndingFrames, IReadOnlyList<WhoWhom> WhoWhom, IReadOnlyList<GenderRule> GenderRules,
    IReadOnlyList<Term> Terms)
{
    public Catalog ToCatalog() => new(Nouns, Rektion, CaseFrames, EndingFrames, WhoWhom, GenderRules, Terms);
}

/// <summary>
/// Compile-time generated JSON (de)serialization for the app's data:
/// no reflection, so it survives trimming in the WebAssembly build and starts faster.
/// </summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    UseStringEnumConverter = true)]
[JsonSerializable(typeof(CatalogData))]
public sealed partial class DataJson : JsonSerializerContext;

internal sealed class SegmentConverter : JsonConverter<Segment>
{
    public override Segment Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String) return new Segment(reader.GetString()!);
        using var doc = JsonDocument.ParseValue(ref reader);
        var o = doc.RootElement;
        return new Segment(o.GetProperty("t").GetString()!, o.GetProperty("term").GetInt32());
    }

    public override void Write(Utf8JsonWriter writer, Segment value, JsonSerializerOptions options)
    {
        if (value.Term is not { } term)
        {
            writer.WriteStringValue(value.Text);
            return;
        }
        writer.WriteStartObject();
        writer.WriteString("t", value.Text);
        writer.WriteNumber("term", term);
        writer.WriteEndObject();
    }
}
