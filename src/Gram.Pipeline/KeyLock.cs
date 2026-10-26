namespace Gram.Pipeline;

/// <summary>
/// Every card key ever released (content/card-keys.txt). The learner's progress is stored under these keys on her phone,
/// so a released key may not simply disappear – a renamed lemma or a reordered enum would silently orphan its progress.
/// A key that is gone must be moved in content/card-key-moves.json: to its new key, whose card takes over the progress,
/// or to null, which drops the progress on purpose.
/// </summary>
internal static class KeyLock
{
    public const string File = "card-keys.txt";

    const string Header = """
        # Every card key ever released, written by `build`. Progress on the learner's phone is stored under these keys.
        # Don't edit: a key that is gone has to be moved in card-key-moves.json instead.
        """;

    public static IReadOnlySet<string> Read(string text) =>
        text.Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0 && !l.StartsWith('#')).ToHashSet(StringComparer.Ordinal);

    /// <summary>The released keys plus the current ones, sorted.</summary>
    public static string Write(IEnumerable<string> released, IEnumerable<string> current) =>
        Header.ReplaceLineEndings("\n") + "\n" + string.Concat(released.Union(current).Distinct().Order(StringComparer.Ordinal).Select(k => k + "\n"));

    /// <summary>Released keys that no card has any more and no move accounts for, and moves that don't make sense.</summary>
    public static List<string> Problems(IReadOnlySet<string> released, IReadOnlyDictionary<string, string?> moves, IReadOnlySet<string> current)
    {
        var problems = new List<string>();
        foreach (var key in released.Where(k => !current.Contains(k)).Order(StringComparer.Ordinal))
        {
            if (!moves.ContainsKey(key))
                problems.Add($"card key \"{key}\" was released but no card has it any more: move it in card-key-moves.json " +
                    "to its new key, or to null to drop its progress");
        }
        foreach (var (from, to) in moves.OrderBy(m => m.Key, StringComparer.Ordinal))
        {
            if (current.Contains(from)) problems.Add($"card-key-moves.json: \"{from}\" is still a card, so its progress can't move");
            if (to is not null && !current.Contains(to)) problems.Add($"card-key-moves.json: \"{from}\" moves to \"{to}\", which is no card");
        }
        return problems;
    }
}
