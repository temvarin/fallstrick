using System.Text.Json;
using System.Text.Json.Serialization;

namespace Gram.Domain;

/// <summary>Where progress is kept (the browser's localStorage in the app, memory in tests).</summary>
public interface IKeyValueStore
{
    string? Read(string key);
    void Write(string key, string value);
}

/// <summary>
/// Persists the learner's progress. The schema number marks the stored shape: progress with another
/// schema is never misread, it starts fresh. There is no migration yet because no released version
/// stored another schema. When the shape changes after a release, add a migration from the
/// released schema here instead of discarding the learner's progress.
/// </summary>
public sealed class ProgressStore(IKeyValueStore store)
{
    private const string Key = "gram.progress";
    private const int Schema = 1;

    public Progress Load() => store.Read(Key) is { } json && TryRead(json) is { } progress
        ? progress
        : new Progress(); // nothing stored yet, or unreadable – start fresh rather than crash

    public void Save(Progress progress) => store.Write(Key, Backup(progress));

    /// <summary>The progress as a backup file: the same format as stored, so a backup is also readable by <see cref="TryRead"/>.</summary>
    public static string Backup(Progress progress) =>
        JsonSerializer.Serialize(new StoredProgress(Schema, progress), ProgressJson.Default.StoredProgress);

    /// <returns>null unless <paramref name="json"/> is progress of this app in the current schema.</returns>
    public static Progress? TryRead(string json)
    {
        try
        {
            var stored = JsonSerializer.Deserialize(json, ProgressJson.Default.StoredProgress);
            return stored is { Schema: Schema, Progress: { Memories: not null } progress } ? progress : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}

internal sealed record StoredProgress(int Schema, Progress Progress);

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    UseStringEnumConverter = true)]
[JsonSerializable(typeof(StoredProgress))]
internal sealed partial class ProgressJson : JsonSerializerContext;
