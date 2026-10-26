using Gram.Domain.Grammar;

namespace Gram.Domain;

/// <summary>
/// The name a card's progress is stored under on the learner's phone: "artikel/Teil/n", "adjektiv/gemischt/dat/m".
/// Released keys must never change (content/card-keys.txt guards that), so the codes below are frozen on purpose: they
/// are neither the codes of the content files nor enum names or positions, which may change without anyone noticing.
/// </summary>
public sealed record CardKey
{
    internal CardKey(string value) => Value = value;

    public string Value { get; }

    public override string ToString() => Value;

    public static CardKey Article(string lemma, Gender gender) => new($"artikel/{lemma}/{Code(gender)}");

    public static CardKey Rektion(string id) => new($"rektion/{id}");

    public static CardKey WhoWhom(string id) => new($"fall/wer/{id}");

    /// <summary>One cell of the article table, after der or after ein (kein shares ein's cells).</summary>
    public static CardKey ArticleForm(bool definite, Case @case, GenderNumber column) =>
        new($"fall/{(definite ? "der" : "ein")}/{Code(@case)}/{Code(column)}");

    /// <summary>One cell of an adjective ending table.</summary>
    public static CardKey Ending(Inflection inflection, Case @case, GenderNumber column) =>
        new($"adjektiv/{Code(inflection)}/{Code(@case)}/{Code(column)}");

    /// <summary>The adjective ending after one quantifier (alle, viele, zwei …), whatever the case.</summary>
    public static CardKey EndingAfter(string quantifier) => new($"adjektiv/nach/{quantifier}");

    static string Code(Gender gender) => gender switch
    {
        Gender.Masculine => "m",
        Gender.Neuter => "n",
        Gender.Feminine => "f",
        _ => throw new ArgumentOutOfRangeException(nameof(gender)),
    };

    static string Code(GenderNumber column) => column switch
    {
        GenderNumber.Masculine => "m",
        GenderNumber.Neuter => "n",
        GenderNumber.Feminine => "f",
        GenderNumber.Plural => "pl",
        _ => throw new ArgumentOutOfRangeException(nameof(column)),
    };

    static string Code(Case @case) => @case switch
    {
        Case.Nominative => "nom",
        Case.Accusative => "akk",
        Case.Dative => "dat",
        Case.Genitive => "gen",
        _ => throw new ArgumentOutOfRangeException(nameof(@case)),
    };

    // The textbook names, kept in keys only: the app never shows them.
    static string Code(Inflection inflection) => inflection switch
    {
        Inflection.Strong => "stark",
        Inflection.Weak => "schwach",
        Inflection.Mixed => "gemischt",
        _ => throw new ArgumentOutOfRangeException(nameof(inflection)),
    };
}
