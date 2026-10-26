namespace Gram.Pipeline;

/// <summary>
/// Every card key ever released (content/card-keys.txt). The learner's progress is stored under these keys on the phone,
/// so a released key may not simply disappear – a renamed lemma or a reordered enum would silently orphan its progress.
/// Dropping a card's progress on purpose means removing its line by hand, which shows in the diff.
/// </summary>
internal static class KeyLock
{
    public const string File = "card-keys.txt";

    const string Header = """
        # Every card key ever released, written by `build`. Progress on the learner's phone is stored under these keys.
        # Remove a line only to drop that card's progress on purpose.
        """;

    public static IReadOnlySet<string> Read(string text) =>
        text.Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0 && !l.StartsWith('#')).ToHashSet(StringComparer.Ordinal);

    /// <summary>The released keys plus the current ones, sorted.</summary>
    public static string Write(IEnumerable<string> released, IEnumerable<string> current) =>
        Header.ReplaceLineEndings("\n") + "\n" + string.Concat(released.Union(current).Distinct().Order(StringComparer.Ordinal).Select(k => k + "\n"));

    /// <summary>Released keys that no card has any more.</summary>
    public static List<string> Problems(IReadOnlySet<string> released, IReadOnlySet<string> current) =>
        [.. released.Where(k => !current.Contains(k)).Order(StringComparer.Ordinal).Select(key =>
            $"card key \"{key}\" was released but no card has it any more: bring the card back, or remove the line from " +
            $"{File} to drop its progress on purpose")];
}
