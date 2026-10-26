using System.Text.Json;
using System.Text.Json.Serialization;
using Gram.Domain.Grammar;

namespace Gram.Domain;

// The app's content as the pipeline writes it to wwwroot/data and the app reads it.

public enum Level { A1, A2, B1, B2, C1 }

/// <summary>A piece of displayed text, optionally a word with a translation (an index into <see cref="Catalog.Terms"/>).</summary>
/// <remarks>Serialized as a plain JSON string when it has no term, to keep the data small.</remarks>
[JsonConverter(typeof(SegmentConverter))]
public sealed record Segment(string Text, int? Term = null);

/// <summary>A sentence with one gap: the text before and after it, and every accepted answer (the first is the usual one).</summary>
public sealed record Gap(IReadOnlyList<Segment> Before, IReadOnlyList<string> Answers, IReadOnlyList<Segment> After);

/// <summary>A word the learner can tap: the German form shown in the card, its Russian translation.</summary>
public sealed record Term(string De, string Ru);

/// <param name="Hint">A short meaning, so the learner knows which word is meant; required where one spelling has two genders.</param>
public sealed record Noun(string Lemma, Gender Gender, string? Plural, string? Hint, string Ru, Level Level)
{
    public static string Article(Gender gender) => gender switch
    {
        Gender.Masculine => "der",
        Gender.Neuter => "das",
        _ => "die",
    };

    [JsonIgnore] public CardKey Key => CardKey.Article(Lemma, Gender);

    [JsonIgnore] public string WithArticle => $"{Article(Gender)} {Lemma}";
}

/// <summary>A verb, adjective or noun with its fixed preposition: bestehen aus + Dativ.</summary>
/// <param name="Meaning">What the combination means, in German.</param>
/// <param name="Hint">A nudge towards the meaning without naming the preposition, shown after a first wrong answer.</param>
/// <param name="Group">Entries with the same headword and different prepositions: bestehen aus / auf / in.</param>
public sealed record RektionEntry(
    string Id, string Headword, string Preposition, Case Case, string Meaning, string Hint, string Ru, Level Level,
    IReadOnlyList<Gap> Sentences, string? Group = null)
{
    [JsonIgnore] public CardKey Key => CardKey.Rektion(Id);
}

/// <summary>
/// A sentence with a noun phrase to complete: the article form (case practice) or, with <see cref="Adjective"/>, the adjective
/// ending. The noun itself is part of <see cref="After"/>; the grammar engine computes the answer.
/// </summary>
/// <param name="Why">Why the case is what it is: "helfen + Dativ", "mit + Dativ".</param>
public sealed record Frame(
    IReadOnlyList<Segment> Before, IReadOnlyList<Segment> After, string Determiner, Case Case, GenderNumber Column, string Why,
    string? Adjective = null);

/// <summary>A sentence whose word order hides who does what; only the article shows it (Den Lehrer sieht der Schüler).</summary>
/// <param name="Question">Who does it: "Wer sieht?"; the options are the two people in the nominative.</param>
public sealed record WhoWhom(string Id, IReadOnlyList<Segment> Sentence, string Question, IReadOnlyList<string> Options, int Answer, string Why)
{
    [JsonIgnore] public CardKey Key => CardKey.WhoWhom(Id);
}

/// <summary>A noun ending that predicts the gender: -ung → die. <see cref="Reliable"/> rules have (almost) no exceptions.</summary>
public sealed record GenderRule(string Suffix, Gender Gender, bool Reliable, string Example);
