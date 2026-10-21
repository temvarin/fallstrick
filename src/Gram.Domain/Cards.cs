namespace Gram.Domain;

public enum Area { Articles, Cases, Rektion, AdjectiveEndings }

/// <summary>One thing to remember, scheduled on its own (see <see cref="Fsrs"/>).</summary>
/// <param name="Key">Stable across releases, because progress is stored under it: "artikel/Teil/n", "rektion/bestehen-aus".</param>
/// <param name="Group">
/// Cards that are easy to confuse, such as der Teil / das Teil or bestehen aus / auf / in. They are introduced
/// on the same day and never shown back to back, so the contrast has to be worked out each time.
/// </param>
public sealed record Card(string Key, Area Area, string? Group = null);
