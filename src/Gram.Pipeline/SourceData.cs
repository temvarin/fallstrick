using Gram.Domain;
using Gram.Domain.Grammar;

namespace Gram.Pipeline;

// The hand-written inputs in content/. Codes are short so the files stay easy to edit:
// gender m / n / f (and pl in frames), case nom / akk / dat / gen, level A2 / B1 / B2 / C1.
// "src" lists ids from sources.json: where the entry was checked.

/// <param name="Forms">Extra inflected forms to make tappable, beyond the ones derived from lemma and plural (des Kollegen).</param>
public sealed record SourceNoun(string De, string G, string? Pl, string? Hint, string Ru, string Level, string[] Src, string[]? Forms = null);

/// <param name="Sentences">Each with one gap in braces holding the answer; alternatives separated by |: "{am|an dem}".</param>
public sealed record SourceRektion(
    string Id, string De, string Prep, string Case, string Meaning, string Hint, string Ru, string Level,
    string[] Sentences, string[] Src, string? Group = null);

/// <param name="Text">With "{}" where the noun phrase starts; the noun itself follows in the text.</param>
public sealed record SourceFrame(string Text, string Det, string Case, string G, string Why, string? Adj = null);

public sealed record SourceWhoWhom(string Id, string Text, string Question, string[] Options, int Answer, string Why);

public sealed record SourceGenderRule(string Suffix, string G, bool Reliable, string Example, string[] Src);

/// <summary>A tappable word that isn't one of the nouns: verbs, adjectives, other nouns.</summary>
public sealed record SourceTerm(string De, string[] Forms, string Ru);

/// <param name="KeyMoves">Released card keys that no card has any more: to the key that takes over their progress, or null (see <see cref="KeyLock"/>).</param>
public sealed record SourceData(
    IReadOnlyDictionary<string, string> Sources,
    IReadOnlyList<SourceNoun> Nouns,
    IReadOnlyList<SourceRektion> Rektion,
    IReadOnlyList<SourceFrame> Frames,
    IReadOnlyList<SourceWhoWhom> WhoWhom,
    IReadOnlyList<SourceGenderRule> GenderRules,
    IReadOnlyList<SourceTerm> Glossary,
    IReadOnlyDictionary<string, string?>? KeyMoves = null)
{
    public static SourceData Load(Paths paths)
    {
        T Read<T>(string name) => Json.Read<T>(Path.Combine(paths.Sources, name));
        return new(
            Read<Dictionary<string, string>>("sources.json"),
            Read<List<SourceNoun>>("nouns.json"),
            Read<List<SourceRektion>>("rektion.json"),
            Read<List<SourceFrame>>("frames.json"),
            Read<List<SourceWhoWhom>>("wer-wen.json"),
            Read<List<SourceGenderRule>>("gender-rules.json"),
            Read<List<SourceTerm>>("glossary.json"),
            Read<Dictionary<string, string?>>("card-key-moves.json"));
    }
}

/// <summary>Turns the short codes into domain values; null for an unknown code (reported by <see cref="Validation"/>).</summary>
internal static class Codes
{
    public static Gender? Gender(string code) => code switch
    {
        "m" => Domain.Grammar.Gender.Masculine,
        "n" => Domain.Grammar.Gender.Neuter,
        "f" => Domain.Grammar.Gender.Feminine,
        _ => null,
    };

    public static GenderNumber? Column(string code) => code == "pl" ? GenderNumber.Plural : (GenderNumber?)Gender(code);

    public static Case? Case(string code) => code switch
    {
        "nom" => Domain.Grammar.Case.Nominative,
        "akk" => Domain.Grammar.Case.Accusative,
        "dat" => Domain.Grammar.Case.Dative,
        "gen" => Domain.Grammar.Case.Genitive,
        _ => null,
    };

    public static Level? Level(string code) => Enum.TryParse<Level>(code, out var level) && Enum.IsDefined(level) ? level : null;
}
