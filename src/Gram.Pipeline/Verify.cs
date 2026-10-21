using System.Text.Json;
using System.Text.RegularExpressions;

namespace Gram.Pipeline;

/// <summary>
/// Checks the nouns against outside sources, locally only (not in CI): gender and plural against de.wiktionary.org,
/// and the level of nouns that cite a Goethe word list against a local copy of that list. Wiktionary pages are cached
/// in .cache/ (not committed: Wiktionary is CC BY-SA, the repository isn't). Reports differences; changes nothing.
/// </summary>
internal sealed partial class Verify(HttpClient http, Paths paths)
{
    string Cache => Path.Combine(paths.Root, ".cache", "wiktionary");

    /// <param name="goethe">A folder with the DWDS copies of the Goethe lists (A1.csv, A2.csv, B1.csv), or null to skip that check.</param>
    /// <returns>Differences, and notes: plurals Wiktionary knows that the content leaves out on purpose (rare ones).</returns>
    public async Task<(List<string> Problems, List<string> Notes)> RunAsync(SourceData src, string? goethe)
    {
        var problems = new List<string>();
        var notes = new List<string>();
        var lists = goethe is null ? null : GoetheLists(goethe);
        await FetchAsync(src.Nouns.Select(n => n.De));

        foreach (var noun in src.Nouns)
        {
            var page = File.ReadAllText(CacheFile(noun.De));
            var (genders, plurals) = Parse(page);
            if (page.Length == 0) problems.Add($"{noun.De}: no Wiktionary page");
            else if (!genders.Contains(noun.G)) problems.Add($"{noun.G} {noun.De}: Wiktionary has gender {string.Join("/", genders)}");
            else if (noun.Pl is not null && !plurals.Contains(noun.Pl)) problems.Add($"{noun.G} {noun.De}: plural {noun.Pl} not in Wiktionary ({string.Join(", ", plurals)})");
            else if (noun.Pl is null && plurals.Count > 0) notes.Add($"{noun.G} {noun.De}: no plural given, Wiktionary has {string.Join(", ", plurals)}");

            if (lists is null) continue;
            foreach (var id in noun.Src.Where(s => s.StartsWith("goethe-", StringComparison.Ordinal)))
            {
                var level = id["goethe-".Length..].ToUpperInvariant();
                var article = Domain.Noun.Article(Codes.Gender(noun.G)!.Value);
                if (!lists.TryGetValue((level, noun.De), out var articles)) problems.Add($"{noun.De}: cites {id} but isn't in that list");
                else if (!articles.Contains(article)) problems.Add($"{article} {noun.De}: {id} lists it as {articles}");
            }
        }
        return (problems, notes);
    }

    /// <summary>Fetches the pages not cached yet, 50 per request (the API's limit), and waits when asked to slow down.</summary>
    async Task FetchAsync(IEnumerable<string> titles)
    {
        Directory.CreateDirectory(Cache);
        var missing = titles.Distinct().Where(t => !File.Exists(CacheFile(t))).ToList();
        foreach (var batch in missing.Chunk(50))
        {
            var url = "https://de.wiktionary.org/w/api.php?action=query&format=json&formatversion=2&prop=revisions&rvprop=content&rvslots=main&titles="
                + Uri.EscapeDataString(string.Join('|', batch));
            using var response = await GetPoliteAsync(url);
            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            foreach (var page in doc.RootElement.GetProperty("query").GetProperty("pages").EnumerateArray())
            {
                var text = page.TryGetProperty("revisions", out var revisions)
                    ? revisions[0].GetProperty("slots").GetProperty("main").GetProperty("content").GetString()!
                    : ""; // no such page: cached as empty so it isn't asked for again
                await File.WriteAllTextAsync(CacheFile(page.GetProperty("title").GetString()!), text);
            }
        }
    }

    async Task<HttpResponseMessage> GetPoliteAsync(string url)
    {
        for (var attempt = 1; ; attempt++)
        {
            var response = await http.GetAsync(url);
            if (response.StatusCode != System.Net.HttpStatusCode.TooManyRequests || attempt == 5) return response.EnsureSuccessStatusCode();
            var wait = response.Headers.RetryAfter?.Delta ?? TimeSpan.FromSeconds(10 * attempt);
            Console.Error.WriteLine($"Wiktionary asks to slow down; waiting {wait.TotalSeconds:0} s");
            response.Dispose();
            await Task.Delay(wait);
        }
    }

    string CacheFile(string title) => Path.Combine(Cache, $"{title}.txt");

    /// <summary>Genders (m, n, f) and nominative plurals of every German noun entry on a page.</summary>
    internal static (HashSet<string> Genders, HashSet<string> Plurals) Parse(string wikitext)
    {
        var german = GermanSection().Matches(wikitext).Select(m => m.Value);
        var genders = new HashSet<string>();
        var plurals = new HashSet<string>();
        foreach (var section in german)
        {
            foreach (Match m in GenusParam().Matches(section)) genders.Add(m.Groups[1].Value);
            foreach (Match m in PluralParam().Matches(section))
            {
                var plural = m.Groups[1].Value.Trim();
                if (plural.Length > 0 && plural != "—" && plural != "-") plurals.Add(plural);
            }
        }
        return (genders, plurals);
    }

    static Dictionary<(string Level, string Lemma), string> GoetheLists(string folder)
    {
        var lists = new Dictionary<(string, string), string>();
        foreach (var level in (string[])["A1", "A2", "B1"])
        {
            var file = Path.Combine(folder, $"{level}.csv");
            if (!File.Exists(file)) continue;
            foreach (var line in File.ReadLines(file).Skip(1))
            {
                var cells = CsvCell().Matches(line).Select(m => m.Groups[1].Value).ToList();
                if (cells.Count >= 5 && cells[2] == "Substantiv") lists[(level, cells[0])] = cells[4];
            }
        }
        return lists;
    }

    // From a "== Wort ({{Sprache|Deutsch}}) ==" heading to the next level-2 heading.
    [GeneratedRegex(@"^== [^=\n]*\{\{Sprache\|Deutsch\}\}[^\n]*==\s*$(.*?)(?=^== |\z)", RegexOptions.Multiline | RegexOptions.Singleline)]
    private static partial Regex GermanSection();

    [GeneratedRegex(@"\|\s*Genus(?: \d)?\s*=\s*([mnf])\b")]
    private static partial Regex GenusParam();

    [GeneratedRegex(@"\|\s*Nominativ Plural(?: \d)?\*?\s*=([^|\n}]*)")]
    private static partial Regex PluralParam();

    [GeneratedRegex("\"([^\"]*)\"")]
    private static partial Regex CsvCell();
}
