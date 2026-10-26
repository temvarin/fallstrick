using System.Text.RegularExpressions;
using Gram.Domain;
using Gram.Domain.Grammar;

namespace Gram.Pipeline;

/// <summary>A sentence split at its one gap "{…}": the text around it and the answers written inside.</summary>
internal sealed record GapText(string Before, IReadOnlyList<string> Answers, string After)
{
    /// <summary>Null unless the text has exactly one gap.</summary>
    public static GapText? Parse(string text)
    {
        var open = text.IndexOf('{', StringComparison.Ordinal);
        var close = text.IndexOf('}', StringComparison.Ordinal);
        if (open < 0 || close < open || text.IndexOf('{', open + 1) >= 0 || text.IndexOf('}', close + 1) >= 0) return null;
        var inside = text[(open + 1)..close];
        var answers = inside.Length == 0 ? [] : inside.Split('|').Select(a => a.Trim()).ToList();
        return new GapText(text[..open], answers, text[(close + 1)..]);
    }
}

/// <summary>
/// The tappable words: every form of every noun and glossary word, mapped to its <see cref="Term"/>. Splitting a text
/// into segments marks the known words; only terms that actually occur end up in the app's data.
/// </summary>
internal sealed partial class Words
{
    readonly Dictionary<string, Term> _byForm = new(StringComparer.Ordinal);
    readonly List<Term> _used = [];
    readonly Dictionary<Term, int> _index = [];

    public Words(IEnumerable<Noun> nouns, IEnumerable<SourceNoun> sources, IEnumerable<SourceTerm> glossary)
    {
        var byForm = new Dictionary<string, List<Noun>>(StringComparer.Ordinal);
        foreach (var (noun, source) in nouns.Zip(sources))
        {
            // Distinct: a plural can look like a singular form (der Artikel, die Artikel; das Team, des Teams).
            var forms = NounForms.Of(noun.Lemma, noun.Gender, noun.Plural).Select(f => f.Form).Concat(source.Forms ?? []).Distinct();
            foreach (var form in forms)
                (byForm.TryGetValue(form, out var list) ? list : byForm[form] = []).Add(noun);
        }

        // One spelling, two genders (der Teil, das Teil): one term showing both.
        foreach (var (form, list) in byForm)
            _byForm[form] = new Term(string.Join(" / ", list.Select(Display)), string.Join(" / ", list.Select(n => n.Ru)));

        foreach (var term in glossary)
            foreach (var form in term.Forms.Append(term.De))
                _byForm[form] = new Term(term.De, term.Ru);
    }

    /// <summary>The terms referenced by the segments made so far, in order of first use.</summary>
    public IReadOnlyList<Term> Used => _used;

    /// <summary>Words met in the segments made so far that have no translation.</summary>
    public IReadOnlyList<string> Untranslated => _untranslated;

    readonly List<string> _untranslated = [];

    public IReadOnlyList<Segment> Segments(string text)
    {
        var segments = new List<Segment>();
        var plain = 0;
        foreach (Match word in Word().Matches(text))
        {
            if (!TryFind(word.Value, out var term))
            {
                _untranslated.Add(word.Value);
                continue;
            }
            if (word.Index > plain) segments.Add(new Segment(text[plain..word.Index]));
            segments.Add(new Segment(word.Value, IndexOf(term)));
            plain = word.Index + word.Length;
        }
        if (plain < text.Length) segments.Add(new Segment(text[plain..]));
        return segments;
    }

    bool TryFind(string word, out Term term)
    {
        if (_byForm.TryGetValue(word, out term!)) return true;
        // A verb or adjective capitalised at the start of a sentence.
        return char.IsUpper(word[0]) && _byForm.TryGetValue(char.ToLowerInvariant(word[0]) + word[1..], out term!);
    }

    int IndexOf(Term term)
    {
        if (_index.TryGetValue(term, out var index)) return index;
        _used.Add(term);
        return _index[term] = _used.Count - 1;
    }

    static string Display(Noun noun) => noun.Plural is null ? noun.WithArticle : $"{noun.WithArticle}, die {noun.Plural}";

    [GeneratedRegex(@"\p{L}+(?:-\p{L}+)*")]
    private static partial Regex Word();
}
