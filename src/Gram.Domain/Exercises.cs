using Gram.Domain.Grammar;

namespace Gram.Domain;

/// <summary>
/// One card turned into a task. Every exercise asks the learner to produce the answer; where the answer space has more than
/// three options, a wrong first answer earns a hint and one more try (with three, a second try would be a coin flip).
/// </summary>
public abstract record Exercise(Card Card)
{
    public abstract bool AllowsRetry { get; }

    public abstract bool IsRight(string answer);

    /// <summary>The nudge after a first wrong answer. Null where there is no second try.</summary>
    public virtual string? Hint(string wrongAnswer) => null;
}

/// <summary>der, die or das: three options, which is the whole answer space, so this is recall rather than recognition.</summary>
/// <param name="Rule">The gender rule matching the noun's ending, which may also be one it breaks.</param>
/// <param name="Contrast">Nouns with the same spelling and another gender (das Teil / der Teil).</param>
public sealed record ArticleExercise(Card Card, Noun Noun, GenderRule? Rule, IReadOnlyList<Noun> Contrast) : Exercise(Card)
{
    public static readonly IReadOnlyList<string> Options = ["der", "die", "das"];

    public string Answer => Noun.Article(Noun.Gender);

    public override bool AllowsRetry => false;

    public override bool IsRight(string answer) => answer == Answer;

    /// <summary>The rule behind the gender, or the rule this noun breaks; null if no rule applies.</summary>
    public string? RuleNote => Rule switch
    {
        null => null,
        { Gender: var g } when g != Noun.Gender =>
            $"Ausnahme: Nomen auf -{Rule.Suffix} sind meistens {German.Gender(Rule.Gender)} ({Rule.Example}), aber {Noun.WithArticle}.",
        { Reliable: true } => $"Regel: Nomen auf -{Rule.Suffix} sind {German.Gender(Rule.Gender)}.",
        _ => $"Faustregel: Nomen auf -{Rule.Suffix} sind meistens {German.Gender(Rule.Gender)}.",
    };
}

/// <summary>Choose the form of the article in a sentence: Ich helfe ___ Mann.</summary>
public sealed record CaseExercise(Card Card, Frame Frame) : Exercise(Card)
{
    Determiner Determiner => Determiners.Get(Frame.Determiner);

    public string Answer => Determiner.Form(Frame.Case, Frame.Column)!;

    /// <summary>Every form of this article, so the answer has to come from memory.</summary>
    public IReadOnlyList<string> Options => Enum.GetValues<Case>()
        .SelectMany(c => Enum.GetValues<GenderNumber>().Select(g => Determiner.Form(c, g)))
        .OfType<string>().Distinct().Order(StringComparer.Ordinal).ToList();

    public override bool AllowsRetry => true;

    public override bool IsRight(string answer) => answer == Answer;

    // Frame.Why already names the case ("mit + Dativ"); the gender completes it.
    public override string Hint(string wrongAnswer) => $"{Frame.Why}, {German.Column(Frame.Column)}. Welche Form hat der Artikel hier?";

    public string Explanation => $"{Frame.Why}, {German.Column(Frame.Column)}: {Answer}";
}

/// <summary>Choose the adjective ending: mit einem gut__ Freund.</summary>
public sealed record EndingExercise(Card Card, Frame Frame) : Exercise(Card)
{
    Determiner Determiner => Determiners.Get(Frame.Determiner);

    string Adjective => Frame.Adjective!;

    /// <summary>The article form, empty without one.</summary>
    public string DeterminerForm => Determiner.Form(Frame.Case, Frame.Column)!;

    public string Stem => Grammar.Adjective.Stem(Adjective);

    public string Answer => Grammar.Adjective.Ending(Determiner.Adjective, Frame.Case, Frame.Column);

    public static IReadOnlyList<string> Options => Grammar.Adjective.AllEndings;

    public override bool AllowsRetry => true;

    public override bool IsRight(string answer) => answer == Answer;

    public override string Hint(string wrongAnswer) => DeterminerForm.Length == 0
        ? $"{Frame.Why}, {German.Column(Frame.Column)}. Kein Artikel – wer zeigt dann das Signal?"
        : $"{Frame.Why}, {German.Column(Frame.Column)}. Zeigt „{DeterminerForm}“ das Signal schon – oder muss das Adjektiv es zeigen?";

    /// <summary>
    /// Why the ending is what it is, by the signal rule: the last letter of der / das / die / dem / den / des shows case and
    /// gender, exactly once – on the article or, where the article doesn't show it, on the adjective.
    /// </summary>
    public string Explanation
    {
        get
        {
            var phrase = $"{DeterminerForm} {Stem}{Answer}".Trim();
            var definite = Determiners.Get("der").Form(Frame.Case, Frame.Column)!;
            var signal = Grammar.Adjective.Signal(Frame.Case, Frame.Column);
            var takesSignal = Grammar.Adjective.ShowsSignal(Determiner.Adjective, Frame.Case, Frame.Column);
            if (Determiner.Lemma.Length == 0)
                return takesSignal
                    ? $"Kein Artikel – das Adjektiv holt das Signal {signal} von „{definite}“: {phrase}."
                    : $"Kein Artikel – aber im Genitiv trägt das Nomen das s schon. Das Adjektiv endet auf -{Answer}: {phrase}.";
            if (Determiner.Adjective == Inflection.Strong)
                return $"„{Determiner.Lemma}“ zählt nicht als Artikel – das Adjektiv holt das Signal {signal} von „{definite}“: {phrase}.";
            if (takesSignal)
                return $"„{DeterminerForm}“ hat kein Signal – das Adjektiv holt das {signal} von „{definite}“: {phrase}.";
            return $"„{DeterminerForm}“ zeigt das Signal {signal} – das Adjektiv endet nur auf -{Answer}: {phrase}.";
        }
    }
}

/// <summary>Who does what in a sentence where only the article shows it.</summary>
public sealed record WhoWhomExercise(Card Card, WhoWhom Item) : Exercise(Card)
{
    public override bool AllowsRetry => false;

    public override bool IsRight(string answer) => answer == Item.Options[Item.Answer];
}

/// <summary>Type the preposition (and the article, where the sentence has one): Die Wohnung besteht ___ drei Zimmern.</summary>
/// <param name="Siblings">The same headword with other prepositions, for contrast.</param>
public sealed record RektionExercise(Card Card, RektionEntry Entry, Gap Sentence, IReadOnlyList<RektionEntry> Siblings) : Exercise(Card)
{
    public override bool AllowsRetry => true;

    public override bool IsRight(string answer) => Sentence.Answers.Any(a => Normalize(a) == Normalize(answer));

    public override string Hint(string wrongAnswer)
    {
        var typed = Normalize(wrongAnswer);
        var sibling = Siblings.FirstOrDefault(s => typed == s.Preposition || typed.StartsWith(s.Preposition + " ", StringComparison.Ordinal));
        if (typed == Entry.Preposition)
            return $"Die Präposition stimmt – jetzt noch der Artikel im {German.Case(Entry.Case)}.";
        if (typed.Split(' ')[0] == Entry.Preposition)
            return $"Die Präposition stimmt – aber {Entry.Preposition} steht hier mit {German.Case(Entry.Case)}.";
        return sibling is null
            ? Entry.Hint
            : $"{Entry.Headword} {sibling.Preposition} heißt: {sibling.Meaning}. Hier: {Entry.Hint}";
    }

    public string Explanation => $"{Entry.Headword} {Entry.Preposition} + {German.Case(Entry.Case)}: {Entry.Meaning}";

    static string Normalize(string text) =>
        string.Join(' ', text.Trim().ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries));
}

/// <summary>German grammar terms for explanations. Culture-free: the app runs without ICU data.</summary>
public static class German
{
    public static string Case(Case @case) => @case switch
    {
        Grammar.Case.Nominative => "Nominativ",
        Grammar.Case.Accusative => "Akkusativ",
        Grammar.Case.Dative => "Dativ",
        _ => "Genitiv",
    };

    public static string Column(GenderNumber column) => column switch
    {
        GenderNumber.Masculine => "maskulin",
        GenderNumber.Neuter => "neutral",
        GenderNumber.Feminine => "feminin",
        _ => "Plural",
    };

    public static string Gender(Gender gender) => Column((GenderNumber)gender);
}
