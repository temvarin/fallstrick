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

    Determiner(string lemma, Inflection adjective, Numbers numbers, Func<Case, GenderNumber, string> form)
    {
        Lemma = lemma;
        Adjective = adjective;
        Numbers = numbers;
        _form = form;
    }

    public string Lemma { get; }
    public Inflection Adjective { get; }
    public Numbers Numbers { get; }

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

    // euer loses its second e before an ending: eure, eurem.
    internal static Determiner LikeEin(string lemma, Numbers numbers = Numbers.Both) =>
        new(lemma, Inflection.Mixed, numbers, (c, g) =>
            EinWord[c, g] is var ending && ending.Length > 0 && lemma == "euer" ? "eur" + ending : lemma + ending);

    internal static Determiner Invariable(string lemma, Numbers numbers) =>
        new(lemma, Inflection.Strong, numbers, (_, _) => lemma);
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
        Determiner.LikeDer("alle", "all", Inflection.Weak, Numbers.PluralOnly),
        Determiner.LikeDer("beide", "beid", Inflection.Weak, Numbers.PluralOnly),
        Determiner.LikeDer("sämtliche", "sämtlich", Inflection.Weak, Numbers.PluralOnly),
        Determiner.LikeDer("viele", "viel", Inflection.Strong, Numbers.PluralOnly),
        Determiner.LikeDer("wenige", "wenig", Inflection.Strong, Numbers.PluralOnly),
        Determiner.LikeDer("einige", "einig", Inflection.Strong, Numbers.PluralOnly),
        Determiner.LikeDer("mehrere", "mehrer", Inflection.Strong, Numbers.PluralOnly),
        Determiner.LikeEin("ein", Numbers.SingularOnly),
        Determiner.LikeEin("kein"),
        Determiner.LikeEin("mein"),
        Determiner.LikeEin("dein"),
        Determiner.LikeEin("sein"),
        Determiner.LikeEin("ihr"),
        Determiner.LikeEin("unser"),
        Determiner.LikeEin("euer"),
        Determiner.Invariable("zwei", Numbers.PluralOnly),
        Determiner.Invariable("drei", Numbers.PluralOnly),
        Determiner.Invariable("vier", Numbers.PluralOnly),
        // No determiner at all: the adjective has to show case and gender itself.
        Determiner.Invariable("", Numbers.Both),
    }.ToDictionary(d => d.Lemma);

    public static IEnumerable<Determiner> All => ByLemma.Values;

    public static Determiner Get(string lemma) => ByLemma[lemma];

    public static bool TryGet(string lemma, out Determiner determiner) => ByLemma.TryGetValue(lemma, out determiner!);
}
