namespace Gram.Domain.Grammar;

public enum Numbers { Both, SingularOnly, PluralOnly }

/// <summary>
/// An article or article word. <see cref="Adjective"/> is the inflection an adjective takes after it.
/// Determiners whose adjective inflection varies in standard German (manche, solche, irgendwelche)
/// are deliberately absent, so every exercise has exactly one right answer.
/// </summary>
public sealed class Determiner
{
    static readonly Table Definite = new("der das die die", "den das die die", "dem dem der den", "des des der der");
    static readonly Table DerWord = new("er es e e", "en es e e", "em em er en", "es es er er");
    static readonly Table EinWord = new("- - e e", "en - e e", "em em er en", "es es er er");

    readonly Func<Case, GenderNumber, string> _form;

    Determiner(string lemma, Inflection adjective, Numbers numbers, Func<Case, GenderNumber, string> form, bool isQuantifier = false)
    {
        Lemma = lemma;
        Adjective = adjective;
        Numbers = numbers;
        IsQuantifier = isQuantifier;
        _form = form;
    }

    public string Lemma { get; }
    public Inflection Adjective { get; }
    public Numbers Numbers { get; }

    /// <summary>
    /// alle, viele, zwei …: a word of quantity after which adjective endings are a classic stumbling block, so it is
    /// practised on its own rather than as one cell of a table.
    /// </summary>
    public bool IsQuantifier { get; }

    /// <summary>The form in this case and column, or null where it doesn't exist (ein in the plural).</summary>
    public string? Form(Case @case, GenderNumber column)
    {
        var plural = column == GenderNumber.Plural;
        if (plural && Numbers == Numbers.SingularOnly || !plural && Numbers == Numbers.PluralOnly) return null;
        return _form(@case, column);
    }

    internal static Determiner TheDefiniteArticle() =>
        new("der", Inflection.Weak, Numbers.Both, (c, g) => Definite[c, g]);

    internal static Determiner LikeDer(string lemma, string stem, Inflection adjective, Numbers numbers) =>
        new(lemma, adjective, numbers, (c, g) => stem + DerWord[c, g]);

    internal static Determiner Quantifier(string lemma, string stem, Inflection adjective) =>
        new(lemma, adjective, Numbers.PluralOnly, (c, g) => stem + DerWord[c, g], isQuantifier: true);

    internal static Determiner Number(string lemma) =>
        new(lemma, Inflection.Strong, Numbers.PluralOnly, (_, _) => lemma, isQuantifier: true);

    // euer loses its second e before an ending: eure, eurem.
    internal static Determiner LikeEin(string lemma, Numbers numbers = Numbers.Both) =>
        new(lemma, Inflection.Mixed, numbers, (c, g) =>
            EinWord[c, g] is var ending && ending.Length > 0 && lemma == "euer" ? "eur" + ending : lemma + ending);

    // No determiner at all: the adjective has to show case and gender itself.
    internal static Determiner None() => new("", Inflection.Strong, Numbers.Both, (_, _) => "");
}

public static class Determiners
{
    static readonly Dictionary<string, Determiner> ByLemma = new Determiner[]
    {
        Determiner.TheDefiniteArticle(),
        Determiner.LikeDer("dieser", "dies", Inflection.Weak, Numbers.Both),
        Determiner.LikeDer("jener", "jen", Inflection.Weak, Numbers.Both),
        Determiner.LikeDer("welcher", "welch", Inflection.Weak, Numbers.Both),
        Determiner.LikeDer("jeder", "jed", Inflection.Weak, Numbers.SingularOnly),
        Determiner.Quantifier("alle", "all", Inflection.Weak),
        Determiner.Quantifier("beide", "beid", Inflection.Weak),
        Determiner.Quantifier("sämtliche", "sämtlich", Inflection.Weak),
        Determiner.Quantifier("viele", "viel", Inflection.Strong),
        Determiner.Quantifier("wenige", "wenig", Inflection.Strong),
        Determiner.Quantifier("einige", "einig", Inflection.Strong),
        Determiner.Quantifier("mehrere", "mehrer", Inflection.Strong),
        Determiner.LikeEin("ein", Numbers.SingularOnly),
        Determiner.LikeEin("kein"),
        Determiner.LikeEin("mein"),
        Determiner.LikeEin("dein"),
        Determiner.LikeEin("sein"),
        Determiner.LikeEin("ihr"),
        Determiner.LikeEin("unser"),
        Determiner.LikeEin("euer"),
        Determiner.Number("zwei"),
        Determiner.Number("drei"),
        Determiner.Number("vier"),
        Determiner.None(),
    }.ToDictionary(d => d.Lemma);

    public static IEnumerable<Determiner> All => ByLemma.Values;

    public static Determiner Get(string lemma) => ByLemma[lemma];

    public static bool TryGet(string lemma, out Determiner determiner) => ByLemma.TryGetValue(lemma, out determiner!);
}
