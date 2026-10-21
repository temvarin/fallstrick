using Gram.Domain.Grammar;

namespace Gram.Domain;

/// <summary>Everything the app practises, and the cards derived from it.</summary>
public sealed class Catalog
{
    /// <summary>Determiners after which adjective endings are a classic stumbling block; each gets its own card.</summary>
    public static readonly IReadOnlySet<string> Quantifiers = new HashSet<string> { "alle", "beide", "sämtliche", "viele", "wenige", "einige", "mehrere", "zwei", "drei", "vier" };

    readonly Dictionary<string, Noun> _nouns;
    readonly Dictionary<string, RektionEntry> _rektion;
    readonly Dictionary<string, WhoWhom> _whoWhom;
    readonly ILookup<string, Frame> _frames;

    public Catalog(
        IReadOnlyList<Noun> nouns, IReadOnlyList<RektionEntry> rektion, IReadOnlyList<Frame> frames,
        IReadOnlyList<WhoWhom> whoWhom, IReadOnlyList<GenderRule> genderRules, IReadOnlyList<Term> terms)
    {
        (Nouns, Rektion, Frames, WhoWhom, GenderRules, Terms) = (nouns, rektion, frames, whoWhom, genderRules, terms);
        _nouns = nouns.ToDictionary(n => n.Key);
        _rektion = rektion.ToDictionary(r => r.Key);
        _whoWhom = whoWhom.ToDictionary(w => w.Key);
        _frames = frames.ToLookup(KeyOf);
        Cards = BuildCards();
    }

    public IReadOnlyList<Noun> Nouns { get; }
    public IReadOnlyList<RektionEntry> Rektion { get; }
    public IReadOnlyList<Frame> Frames { get; }
    public IReadOnlyList<WhoWhom> WhoWhom { get; }
    public IReadOnlyList<GenderRule> GenderRules { get; }
    public IReadOnlyList<Term> Terms { get; }

    /// <summary>All cards, in the order new ones are introduced: easier levels and basic cases first.</summary>
    public IReadOnlyList<Card> Cards { get; }

    /// <summary>The card a frame practises: one cell of a declension table, or one quantifier.</summary>
    public static string KeyOf(Frame frame)
    {
        var cell = $"{CaseCode(frame.Case)}/{ColumnCode(frame.Column)}";
        if (frame.Adjective is null)
            return $"fall/{(frame.Determiner == "der" ? "der" : "ein")}/{cell}";
        if (Quantifiers.Contains(frame.Determiner))
            return $"adjektiv/nach/{frame.Determiner}";
        return $"adjektiv/{InflectionCode(Determiners.Get(frame.Determiner).Adjective)}/{cell}";
    }

    public Exercise Exercise(Card card, Random random)
    {
        if (_nouns.TryGetValue(card.Key, out var noun))
            return new ArticleExercise(card, noun, GenderRuleFor(noun), Nouns.Where(n => n.Lemma == noun.Lemma && n != noun).ToList());
        if (_rektion.TryGetValue(card.Key, out var entry))
            return new RektionExercise(card, entry, entry.Sentences[random.Next(entry.Sentences.Count)],
                Rektion.Where(r => r.Group is not null && r.Group == entry.Group && r != entry).ToList());
        if (_whoWhom.TryGetValue(card.Key, out var whoWhom))
            return new WhoWhomExercise(card, whoWhom);

        var frames = _frames[card.Key].ToList();
        var frame = frames[random.Next(frames.Count)];
        return frame.Adjective is null ? new CaseExercise(card, frame) : new EndingExercise(card, frame);
    }

    /// <summary>
    /// The gender rule whose ending the noun has (longest ending wins), or null. The rules are about endings that
    /// follow a stem, so one-syllable words don't count: der See has no "-e" ending, das Ei no "-ei".
    /// </summary>
    public GenderRule? GenderRuleFor(Noun noun) => Syllables(noun.Lemma) < 2 ? null : GenderRules
        .Where(r => noun.Lemma.EndsWith(r.Suffix, StringComparison.OrdinalIgnoreCase))
        .MaxBy(r => r.Suffix.Length);

    // Groups of vowels: Kä-se has two, See one.
    static int Syllables(string word) =>
        word.ToLowerInvariant().Select(c => "aeiouäöüy".Contains(c)).Aggregate((Count: 0, Previous: false),
            (acc, vowel) => (acc.Count + (vowel && !acc.Previous ? 1 : 0), vowel)).Count;

    List<Card> BuildCards()
    {
        var sameLemma = Nouns.GroupBy(n => n.Lemma).Where(g => g.Count() > 1).Select(g => g.Key).ToHashSet();
        var articles = Nouns.OrderBy(n => n.Level)
            .Select(n => new Card(n.Key, Area.Articles, sameLemma.Contains(n.Lemma) ? $"artikel:{n.Lemma}" : null));

        // Definite before indefinite, nominative and accusative first; the who-does-what sentences right after the accusative.
        var caseCells = _frames.Select(g => g.Key).Where(k => k.StartsWith("fall/", StringComparison.Ordinal))
            .OrderBy(k => k.StartsWith("fall/ein", StringComparison.Ordinal)).ThenBy(CaseRank).ToList();
        var basic = caseCells.Where(k => k.StartsWith("fall/der", StringComparison.Ordinal) && CaseRank(k) <= 1);
        var cases = basic.Select(k => new Card(k, Area.Cases))
            .Concat(WhoWhom.Select(w => new Card(w.Key, Area.Cases)))
            .Concat(caseCells.Except(basic).Select(k => new Card(k, Area.Cases)));

        var rektion = Rektion.OrderBy(r => r.Level).Select(r => new Card(r.Key, Area.Rektion, r.Group is null ? null : $"rektion:{r.Group}"));

        // Weak (after der) and mixed (after ein) before strong; quantifiers last.
        string[] order = ["schwach", "gemischt", "stark", "nach"];
        var endings = _frames.Select(g => g.Key).Where(k => k.StartsWith("adjektiv/", StringComparison.Ordinal))
            .OrderBy(k => Array.IndexOf(order, k.Split('/')[1])).ThenBy(CaseRank)
            .Select(k => new Card(k, Area.AdjectiveEndings));

        return [.. articles, .. cases, .. rektion, .. endings];
    }

    static int CaseRank(string key) => Array.IndexOf(["nom", "akk", "dat", "gen"], key.Split('/').ElementAtOrDefault(2));

    static string CaseCode(Case @case) => @case switch
    {
        Case.Nominative => "nom",
        Case.Accusative => "akk",
        Case.Dative => "dat",
        _ => "gen",
    };

    static string ColumnCode(GenderNumber column) => column switch
    {
        GenderNumber.Masculine => "m",
        GenderNumber.Neuter => "n",
        GenderNumber.Feminine => "f",
        _ => "pl",
    };

    static string InflectionCode(Inflection inflection) => inflection switch
    {
        Inflection.Strong => "stark",
        Inflection.Weak => "schwach",
        _ => "gemischt",
    };
}
