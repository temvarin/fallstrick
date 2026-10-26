using Gram.Domain.Grammar;

namespace Gram.Domain.Tests;

public class ExerciseTests
{
    static Segment[] Text(string text) => [new Segment(text)];

    static Frame Frame(string det, Case @case, GenderNumber column, string? adjective = null, string why = "mit + Dativ") =>
        new(Text("Ich fahre mit "), Text(" Freund weg."), det, @case, column, why, adjective);

    static readonly Noun TeilM = new("Teil", Gender.Masculine, "Teile", "ein Stück vom Ganzen", "часть", Level.A1);
    static readonly Noun TeilN = new("Teil", Gender.Neuter, "Teile", "ein Gegenstand", "деталь", Level.B1);
    static readonly Noun Zeitung = new("Zeitung", Gender.Feminine, "Zeitungen", null, "газета", Level.A1);
    static readonly Noun Messer = new("Messer", Gender.Neuter, "Messer", null, "нож", Level.A2);

    static readonly GenderRule[] Rules =
    [
        new("ung", Gender.Feminine, true, "die Zeitung"),
        new("er", Gender.Masculine, false, "der Lehrer"),
    ];

    static RektionEntry Bestehen(string id, string prep, string meaning, string hint, string answer) =>
        new(id, "bestehen", prep, Case.Dative, meaning, hint, "", Level.B1,
            [new Gap(Text("Die Wohnung besteht "), [answer], Text(" drei Zimmern."))], "bestehen");

    static readonly RektionEntry Aus = Bestehen("aus", "aus", "sich zusammensetzen aus", "Es geht um Bestandteile.", "aus");
    static readonly RektionEntry Auf = Bestehen("auf", "auf", "etwas unbedingt fordern", "Es geht um eine Forderung.", "auf");

    static Catalog Catalog(params Frame[] frames) => new([TeilM, TeilN, Zeitung, Messer], [Aus, Auf], frames, [], Rules, []);

    [Fact]
    public void Nouns_come_by_level_and_the_same_spelling_makes_one_group_of_confusable_cards()
    {
        var cards = Catalog().Cards.Where(c => c.Area == Area.Articles).ToList();

        Assert.Equal(["artikel/Teil/m", "artikel/Zeitung/f", "artikel/Messer/n", "artikel/Teil/n"], cards.Select(c => c.Key.Value));
        Assert.Equal(["artikel:Teil", null, null, "artikel:Teil"], cards.Select(c => c.Group));
    }

    [Fact]
    public void Frames_become_one_card_per_table_cell_and_one_per_quantifier()
    {
        var catalog = Catalog(
            Frame("ein", Case.Dative, GenderNumber.Masculine, "gut"),
            Frame("kein", Case.Dative, GenderNumber.Masculine, "alt"),
            Frame("der", Case.Dative, GenderNumber.Masculine),
            Frame("alle", Case.Dative, GenderNumber.Plural, "neu"));

        Assert.Equal(["fall/der/dat/m", "adjektiv/gemischt/dat/m", "adjektiv/nach/alle"],
            catalog.Cards.Where(c => c.Area is Area.Cases or Area.AdjectiveEndings).Select(c => c.Key.Value));
    }

    [Fact]
    public void An_article_exercise_names_the_rule_or_the_rule_the_noun_breaks()
    {
        var catalog = Catalog();
        string? Note(Noun noun) => ((ArticleExercise)catalog.Exercise(new Card(noun.Key, Area.Articles), new Random(1))).RuleNote;

        Assert.Equal("Regel: Nomen auf -ung sind feminin.", Note(Zeitung));
        Assert.Equal("Ausnahme: Nomen auf -er sind meistens maskulin (der Lehrer), aber das Messer.", Note(Messer));
        Assert.Null(Note(TeilM));
    }

    [Fact]
    public void Endings_are_rules_only_in_words_of_more_than_one_syllable()
    {
        var rules = new Catalog([], [], [], [], [new GenderRule("e", Gender.Feminine, false, "die Lampe")], []);

        Assert.Null(rules.GenderRuleFor(new Noun("See", Gender.Masculine, "Seen", null, "", Level.A1)));
        Assert.NotNull(rules.GenderRuleFor(new Noun("Käse", Gender.Masculine, "Käse", null, "", Level.A2)));
    }

    [Fact]
    public void An_article_exercise_shows_the_other_gender_of_the_same_spelling()
    {
        var exercise = (ArticleExercise)Catalog().Exercise(new Card(TeilN.Key, Area.Articles), new Random(1));

        Assert.Equal("das", exercise.Answer);
        Assert.Equal([TeilM], exercise.Contrast);
        Assert.False(exercise.AllowsRetry);
    }

    [Fact]
    public void A_case_exercise_offers_every_form_of_its_article()
    {
        var exercise = new CaseExercise(new Card(CardKey.ArticleForm(false, Case.Dative, GenderNumber.Masculine), Area.Cases), Frame("ein", Case.Dative, GenderNumber.Masculine));

        Assert.Equal("einem", exercise.Answer);
        Assert.Equal(["ein", "eine", "einem", "einen", "einer", "eines"], exercise.Options);
        Assert.Equal("mit + Dativ, maskulin: einem", exercise.Explanation);
        Assert.Equal("mit + Dativ, maskulin. Welche Form hat der Artikel hier?", exercise.Hint("ein"));
    }

    [Theory]
    [InlineData("der", Case.Dative, GenderNumber.Masculine, "„dem“ zeigt das Signal m – das Adjektiv endet nur auf -en: dem guten.")]
    [InlineData("ein", Case.Nominative, GenderNumber.Masculine, "„ein“ hat kein Signal – das Adjektiv holt das r von „der“: ein guter.")]
    [InlineData("ein", Case.Accusative, GenderNumber.Neuter, "„ein“ hat kein Signal – das Adjektiv holt das s von „das“: ein gutes.")]
    [InlineData("ein", Case.Dative, GenderNumber.Masculine, "„einem“ zeigt das Signal m – das Adjektiv endet nur auf -en: einem guten.")]
    [InlineData("", Case.Dative, GenderNumber.Masculine, "Kein Artikel – das Adjektiv holt das Signal m von „dem“: gutem.")]
    [InlineData("", Case.Genitive, GenderNumber.Neuter, "Kein Artikel – aber im Genitiv trägt das Nomen das s schon. Das Adjektiv endet auf -en: guten.")]
    [InlineData("viele", Case.Nominative, GenderNumber.Plural, "„viele“ zählt nicht als Artikel – das Adjektiv holt das Signal e von „die“: viele gute.")]
    [InlineData("alle", Case.Nominative, GenderNumber.Plural, "„alle“ zeigt das Signal e – das Adjektiv endet nur auf -en: alle guten.")]
    public void An_ending_explanation_says_who_shows_the_signal(string det, Case @case, GenderNumber column, string expected)
    {
        var exercise = new EndingExercise(new Card(Gram.Domain.Catalog.KeyOf(Frame(det, @case, column, "gut")), Area.AdjectiveEndings), Frame(det, @case, column, "gut"));

        Assert.Equal(expected, exercise.Explanation);
        Assert.True(exercise.IsRight(Adjective.Ending(Determiners.Get(det).Adjective, @case, column)));
    }

    [Theory]
    [InlineData("aus", true)]
    [InlineData("  AUS ", true)]
    [InlineData("auf", false)]
    public void Typed_answers_ignore_case_and_spaces(string typed, bool right)
    {
        var exercise = (RektionExercise)Catalog().Exercise(new Card(Aus.Key, Area.Rektion), new Random(1));
        Assert.Equal(right, exercise.IsRight(typed));
    }

    [Fact]
    public void A_sibling_preposition_gets_a_hint_that_contrasts_both_meanings()
    {
        var exercise = (RektionExercise)Catalog().Exercise(new Card(Aus.Key, Area.Rektion), new Random(1));

        Assert.Equal("bestehen auf heißt: etwas unbedingt fordern. Hier: Es geht um Bestandteile.", exercise.Hint("auf"));
        Assert.Equal("Es geht um Bestandteile.", exercise.Hint("von"));
        Assert.Equal("bestehen aus + Dativ: sich zusammensetzen aus", exercise.Explanation);
    }

    [Fact]
    public void The_right_preposition_without_the_article_asks_for_the_article()
    {
        var entry = Aus with { Sentences = [new Gap(Text("Er hilft "), ["aus dem"], Text(" Haus."))] };
        var exercise = new RektionExercise(new Card(entry.Key, Area.Rektion), entry, entry.Sentences[0], []);

        Assert.Equal("Die Präposition stimmt – jetzt noch der Artikel im Dativ.", exercise.Hint("aus"));
        Assert.Equal("Die Präposition stimmt – aber aus steht hier mit Dativ.", exercise.Hint("aus den"));
    }
}
