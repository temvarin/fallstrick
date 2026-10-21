using System.Diagnostics.CodeAnalysis;
using Gram.Domain;

namespace Gram.Pipeline;

/// <summary>The command-line entry points. Each returns the process exit code.</summary>
internal static class Commands
{
    const string CatalogFile = "catalog.json";

    /// <summary>Validates the sources and writes wwwroot/data/catalog.json.</summary>
    public static int Build(Paths paths)
    {
        if (!TryBuild(paths, out var json, out var catalog)) return 1;

        Directory.CreateDirectory(paths.AppData);
        File.WriteAllText(Path.Combine(paths.AppData, CatalogFile), json);
        Console.WriteLine($"wwwroot/data/{CatalogFile}: {catalog.Nouns.Count} nouns, {catalog.Rektion.Count} Rektion entries, " +
            $"{catalog.Frames.Count} frames, {catalog.WhoWhom.Count} who-whom sentences, {catalog.Cards.Count} cards");
        return 0;
    }

    /// <summary>Validates the sources and verifies that wwwroot/data is exactly what <see cref="Build"/> would write (used in CI).</summary>
    public static int Check(Paths paths)
    {
        if (!TryBuild(paths, out var json, out _)) return 1;

        var file = Path.Combine(paths.AppData, CatalogFile);
        if (!File.Exists(file) || File.ReadAllText(file) != json)
        {
            Console.Error.WriteLine($"wwwroot/data/{CatalogFile} is out of date – run `build`.");
            return 1;
        }
        Console.WriteLine("Sources valid, wwwroot/data up to date.");
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

    static bool TryBuild(Paths paths, [NotNullWhen(true)] out string? json, [NotNullWhen(true)] out Catalog? catalog)
    {
        var src = SourceData.Load(paths);
        var problems = Validation.Check(src);
        if (problems.Count > 0)
        {
            Console.Error.WriteLine($"{problems.Count} problem(s) in content/:\n  " + string.Join("\n  ", problems));
            (json, catalog) = (null, null);
            return false;
        }
        var data = DataBuilder.Build(src);
        (json, catalog) = (Json.Write(data), data.ToCatalog());
        return true;
    }
}
