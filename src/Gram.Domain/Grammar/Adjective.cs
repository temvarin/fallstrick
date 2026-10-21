namespace Gram.Domain.Grammar;

public static class Adjective
{
    /// <summary>Every ending an attributive adjective can take; the full answer space of an exercise.</summary>
    public static readonly IReadOnlyList<string> AllEndings = ["e", "en", "er", "es", "em"];

    static readonly Table Strong = new("er es e e", "en es e e", "em em er en", "en en er er");
    static readonly Table Weak = new("e e e en", "en e e en", "en en en en", "en en en en");
    static readonly Table Mixed = new("er es e en", "en es e en", "en en en en", "en en en en");

    public static string Ending(Inflection inflection, Case @case, GenderNumber column) => inflection switch
    {
        Inflection.Strong => Strong[@case, column],
        Inflection.Weak => Weak[@case, column],
        _ => Mixed[@case, column],
    };

    /// <summary>The stem an ending attaches to: hoch → hoh, dunkel → dunkl, teuer → teur, leise → leis.</summary>
    public static string Stem(string adjective)
    {
        if (adjective == "hoch") return "hoh";
        if (adjective.EndsWith('e')) return adjective[..^1];
        if (adjective.EndsWith("el", StringComparison.Ordinal)) return adjective[..^2] + "l";
        if (adjective.EndsWith("auer", StringComparison.Ordinal) || adjective.EndsWith("euer", StringComparison.Ordinal))
            return adjective[..^2] + "r";
        return adjective;
    }

    /// <summary>The signal of a case and column: the last letter of the definite article (der → r, dem → m, die → e).</summary>
    public static char Signal(Case @case, GenderNumber column) => Determiners.Get("der").Form(@case, column)![^1];

    /// <summary>
    /// Whether the adjective itself shows the signal here, because the article doesn't: always without an article (except
    /// the genitive masculine and neuter, whose noun carries the s), and after ein in its three endingless forms.
    /// </summary>
    public static bool ShowsSignal(Inflection inflection, Case @case, GenderNumber column) => inflection switch
    {
        Inflection.Strong => !(@case == Case.Genitive && column is GenderNumber.Masculine or GenderNumber.Neuter),
        Inflection.Mixed => Ending(inflection, @case, column) is "er" or "es",
        _ => false,
    };

    public static string Inflect(string adjective, Inflection inflection, Case @case, GenderNumber column) =>
        Stem(adjective) + Ending(inflection, @case, column);
}
