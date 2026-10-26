using Gram.Domain;

namespace Gram.Pipeline;

/// <summary>Turns validated sources into the app's catalog. Pure: no file access, so tests can call it directly.</summary>
internal static class DataBuilder
{
    public static CatalogData Build(SourceData src) => Build(src, out _);

    /// <param name="untranslated">Words in the sentences that can't be tapped yet, for the `words` report.</param>
    public static CatalogData Build(SourceData src, out IReadOnlyList<string> untranslated)
    {
        var nouns = src.Nouns
            .Select(n => new Noun(n.De, Codes.Gender(n.G)!.Value, n.Pl, n.Hint, n.Ru, Codes.Level(n.Level)!.Value))
            .ToList();
        var words = new Words(nouns, src.Nouns, src.Glossary);

        Gap Gap(string text)
        {
            var gap = GapText.Parse(text)!;
            return new Gap(words.Segments(gap.Before), gap.Answers, words.Segments(gap.After));
        }

        var rektion = src.Rektion.Select(r => new RektionEntry(
            r.Id, r.De, r.Prep, Codes.Case(r.Case)!.Value, r.Meaning, r.Hint, r.Ru, Codes.Level(r.Level)!.Value,
            r.Sentences.Select(Gap).ToList(), r.Group)).ToList();

        // In the order of frames.json, so tappable words are numbered as they come.
        var frames = src.Frames.Select(f =>
        {
            var gap = Gap(f.Text);
            var (@case, column) = (Codes.Case(f.Case)!.Value, Codes.Column(f.G)!.Value);
            return f.Adj is null
                ? (Frame)new CaseFrame(gap.Before, gap.After, f.Det, @case, column, f.Why)
                : new EndingFrame(gap.Before, gap.After, f.Det, @case, column, f.Why, f.Adj);
        }).ToList();

        var whoWhom = src.WhoWhom.Select(w => new WhoWhom(w.Id, words.Segments(w.Text), w.Question, w.Options, w.Answer, w.Why)).ToList();

        var rules = src.GenderRules.Select(r => new GenderRule(r.Suffix, Codes.Gender(r.G)!.Value, r.Reliable, r.Example)).ToList();

        untranslated = words.Untranslated;
        return new CatalogData(nouns, rektion, [.. frames.OfType<CaseFrame>()], [.. frames.OfType<EndingFrame>()], whoWhom, rules, words.Used.ToList());
    }
}
