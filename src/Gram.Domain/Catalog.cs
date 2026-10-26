using Gram.Domain.Grammar;

namespace Gram.Domain;

/// <summary>Everything the app practises, and the cards derived from it.</summary>
public sealed class Catalog
{
    readonly Dictionary<CardKey, Noun> _nouns;
    readonly Dictionary<CardKey, RektionEntry> _rektion;
    readonly Dictionary<CardKey, WhoWhom> _whoWhom;
    readonly ILookup<CardKey, CaseFrame> _caseFrames;
    readonly ILookup<CardKey, EndingFrame> _endingFrames;

    public Catalog(
        IReadOnlyList<Noun> nouns, IReadOnlyList<RektionEntry> rektion, IReadOnlyList<CaseFrame> caseFrames,
        IReadOnlyList<EndingFrame> endingFrames, IReadOnlyList<WhoWhom> whoWhom, IReadOnlyList<GenderRule> genderRules,
        IReadOnlyList<Term> terms, IReadOnlyDictionary<string, string>? keyMoves = null)
    {
        (Nouns, Rektion, CaseFrames, EndingFrames, WhoWhom) = (nouns, rektion, caseFrames, endingFrames, whoWhom);
        (GenderRules, Terms) = (genderRules, terms);
        KeyMoves = keyMoves ?? new Dictionary<string, string>();
        _nouns = nouns.ToDictionary(n => n.Key);
        _rektion = rektion.ToDictionary(r => r.Key);
        _whoWhom = whoWhom.ToDictionary(w => w.Key);
        _caseFrames = caseFrames.ToLookup(KeyOf);
        _endingFrames = endingFrames.ToLookup(KeyOf);
        Cards = BuildCards();
    }

    public IReadOnlyList<Noun> Nouns { get; }
    public IReadOnlyList<RektionEntry> Rektion { get; }
    public IReadOnlyList<CaseFrame> CaseFrames { get; }
    public IReadOnlyList<EndingFrame> EndingFrames { get; }
    public IReadOnlyList<WhoWhom> WhoWhom { get; }
    public IReadOnlyList<GenderRule> GenderRules { get; }
    public IReadOnlyList<Term> Terms { get; }

    /// <summary>Card keys of earlier releases, to the card that takes over their progress (see <see cref="Progress.Move"/>).</summary>
    public IReadOnlyDictionary<string, string> KeyMoves { get; }

    /// <summary>All cards, in the order new ones are introduced: easier levels and basic cases first.</summary>
    public IReadOnlyList<Card> Cards { get; }

    /// <summary>The card a case frame practises: one cell of the article table after der, or after ein and kein.</summary>
    public static CardKey KeyOf(CaseFrame frame) => CardKey.ArticleForm(frame.Determiner == "der", frame.Case, frame.Column);

    /// <summary>The card an ending frame practises: one cell of an ending table, or one quantifier.</summary>
    public static CardKey KeyOf(EndingFrame frame) => Determiners.Get(frame.Determiner) is { IsQuantifier: true }
        ? CardKey.EndingAfter(frame.Determiner)
        : CardKey.Ending(Determiners.Get(frame.Determiner).Adjective, frame.Case, frame.Column);

    public Exercise Exercise(Card card, Random random)
    {
        switch (card.Area)
        {
            case Area.Articles:
                var noun = _nouns[card.Key];
                return new ArticleExercise(card, noun, GenderRuleFor(noun), Nouns.Where(n => n.Lemma == noun.Lemma && n != noun).ToList());
            case Area.Rektion:
                var entry = _rektion[card.Key];
                return new RektionExercise(card, entry, entry.Sentences[random.Next(entry.Sentences.Count)],
                    Rektion.Where(r => r.Group is not null && r.Group == entry.Group && r != entry).ToList());
            case Area.Cases when _whoWhom.TryGetValue(card.Key, out var whoWhom):
                return new WhoWhomExercise(card, whoWhom);
            case Area.Cases:
                return new CaseExercise(card, Pick(_caseFrames[card.Key].ToList(), random));
            case Area.AdjectiveEndings:
                return new EndingExercise(card, Pick(_endingFrames[card.Key].ToList(), random));
            default:
                throw new ArgumentOutOfRangeException(nameof(card), card.Area, "No exercise for this area.");
        }
    }

    // Sentences rotate within a card: another one each time, at random.
    static T Pick<T>(List<T> items, Random random) => items[random.Next(items.Count)];

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

        // One card per key; its frames share determiner kind, case and column, so the first one stands for all.
        // Definite before indefinite, nominative and accusative first; the who-does-what sentences right after the accusative.
        var caseCells = _caseFrames.Select(g => (Key: g.Key, Frame: g.First()))
            .OrderBy(c => c.Frame.Determiner != "der").ThenBy(c => c.Frame.Case).ToList();
        var basic = caseCells.Where(c => c.Frame.Determiner == "der" && c.Frame.Case is Case.Nominative or Case.Accusative).ToList();
        var cases = basic.Select(c => new Card(c.Key, Area.Cases))
            .Concat(WhoWhom.Select(w => new Card(w.Key, Area.Cases)))
            .Concat(caseCells.Except(basic).Select(c => new Card(c.Key, Area.Cases)));

        var rektion = Rektion.OrderBy(r => r.Level).Select(r => new Card(r.Key, Area.Rektion, r.Group is null ? null : $"rektion:{r.Group}"));

        // Weak (after der) and mixed (after ein) before strong, each by case; quantifiers last, in the order of the frames.
        var endings = _endingFrames.Select(g => (g.Key, Determiner: Determiners.Get(g.First().Determiner), g.First().Case))
            .OrderBy(c => EndingRank(c.Determiner)).ThenBy(c => c.Determiner.IsQuantifier ? null : (Case?)c.Case)
            .Select(c => new Card(c.Key, Area.AdjectiveEndings));

        return [.. articles, .. cases, .. rektion, .. endings];
    }

    static int EndingRank(Determiner determiner) => determiner.IsQuantifier ? 3 : determiner.Adjective switch
    {
        Inflection.Weak => 0,
        Inflection.Mixed => 1,
        _ => 2,
    };
}
