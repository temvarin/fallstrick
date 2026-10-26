using System.Diagnostics.CodeAnalysis;
using Gram.Domain;

namespace Gram.Pipeline;

/// <summary>The command-line entry points. Each returns the process exit code.</summary>
internal static class Commands
{
    const string CatalogFile = "catalog.json";

    /// <summary>Validates the sources and writes wwwroot/data/catalog.json, adding new card keys to content/card-keys.txt.</summary>
    public static int Build(Paths paths)
    {
        if (!TryBuild(paths, out var json, out var catalog, out var keys)) return 1;

        Directory.CreateDirectory(paths.AppData);
        File.WriteAllText(Path.Combine(paths.AppData, CatalogFile), json);
        File.WriteAllText(Path.Combine(paths.Sources, KeyLock.File), keys);
        Console.WriteLine($"wwwroot/data/{CatalogFile}: {catalog.Nouns.Count} nouns, {catalog.Rektion.Count} Rektion entries, " +
            $"{catalog.Frames.Count} frames, {catalog.WhoWhom.Count} who-whom sentences, {catalog.Cards.Count} cards");
        return 0;
    }

    /// <summary>
    /// Validates the sources and verifies that wwwroot/data and content/card-keys.txt are exactly what <see cref="Build"/>
    /// would write (used in CI).
    /// </summary>
    public static int Check(Paths paths)
    {
        if (!TryBuild(paths, out var json, out _, out var keys)) return 1;

        foreach (var (file, expected) in new[] { (Path.Combine(paths.AppData, CatalogFile), json), (Path.Combine(paths.Sources, KeyLock.File), keys) })
        {
            // Line endings as Git checked them out (CRLF on Windows with core.autocrlf) don't count.
            if (File.Exists(file) && File.ReadAllText(file).ReplaceLineEndings("\n") == expected) continue;
            Console.Error.WriteLine($"{Path.GetRelativePath(paths.Root, file).Replace('\\', '/')} is out of date – run `build`.");
            return 1;
        }
        Console.WriteLine("Sources valid, wwwroot/data and card keys up to date.");
        return 0;
    }

    public static async Task<int> VerifyAsync(Paths paths, string? goethe)
    {
        using var http = new HttpClient();
        // Wikimedia asks API clients to identify themselves.
        http.DefaultRequestHeaders.UserAgent.ParseAdd("FallstrickPipeline/1.0 (content check of a learning app)");
        var (problems, notes) = await new Verify(http, paths).RunAsync(SourceData.Load(paths), goethe);
        foreach (var note in notes) Console.WriteLine($"note: {note}");
        foreach (var problem in problems) Console.WriteLine(problem);
        Console.WriteLine(problems.Count == 0 ? "All nouns match." : $"{problems.Count} difference(s) to check.");
        return problems.Count == 0 ? 0 : 1;
    }

    /// <summary>Lists the words in sentences that can't be tapped for a translation, most frequent first, leaving out content/common-words.txt.</summary>
    public static int Words(Paths paths)
    {
        var common = File.ReadAllLines(Path.Combine(paths.Sources, "common-words.txt"))
            .Where(l => l.Length > 0 && !l.StartsWith('#')).SelectMany(l => l.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        DataBuilder.Build(SourceData.Load(paths), out var untranslated);
        var missing = untranslated.Where(w => !common.Contains(w)).GroupBy(w => w).OrderByDescending(g => g.Count()).ThenBy(g => g.Key, StringComparer.Ordinal).ToList();
        foreach (var word in missing) Console.WriteLine($"{word.Count(),3} {word.Key}");
        Console.WriteLine($"{missing.Count} words without a translation.");
        return 0;
    }

    /// <param name="keys">content/card-keys.txt as it is after this build.</param>
    static bool TryBuild(
        Paths paths, [NotNullWhen(true)] out string? json, [NotNullWhen(true)] out Catalog? catalog, [NotNullWhen(true)] out string? keys)
    {
        (json, catalog, keys) = (null, null, null);
        var src = SourceData.Load(paths);
        var problems = Validation.Check(src);
        if (problems.Count > 0) return Fail(problems);

        var data = DataBuilder.Build(src);
        var built = data.ToCatalog();
        var lockFile = Path.Combine(paths.Sources, KeyLock.File);
        var released = File.Exists(lockFile) ? KeyLock.Read(File.ReadAllText(lockFile)) : new HashSet<string>();
        var current = built.Cards.Select(c => c.Key.ToString()).ToHashSet(StringComparer.Ordinal);
        problems = KeyLock.Problems(released, src.KeyMoves ?? new Dictionary<string, string?>(), current);
        if (problems.Count > 0) return Fail(problems);

        (json, catalog, keys) = (Json.Write(data), built, KeyLock.Write(released, current));
        return true;
    }

    static bool Fail(List<string> problems)
    {
        Console.Error.WriteLine($"{problems.Count} problem(s) in content/:\n  " + string.Join("\n  ", problems));
        return false;
    }
}
