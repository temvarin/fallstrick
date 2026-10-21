using Gram.Domain.Grammar;

namespace Gram.Domain.Tests;

// The expected tables are the standard paradigms (Duden 4, Die Grammatik).
// Each row lists masculine, neuter, feminine, plural; "–" means the form doesn't exist.
public class DeclensionTests
{
    static void AssertRow(Func<GenderNumber, string?> form, string expected)
    {
        var actual = Enum.GetValues<GenderNumber>().Select(c => form(c) ?? "–");
        Assert.Equal(expected, string.Join(' ', actual));
    }

    [Theory]
    [InlineData(Case.Nominative, "der das die die")]
    [InlineData(Case.Accusative, "den das die die")]
    [InlineData(Case.Dative, "dem dem der den")]
    [InlineData(Case.Genitive, "des des der der")]
    public void The_definite_article(Case @case, string row) =>
        AssertRow(c => Determiners.Get("der").Form(@case, c), row);

    [Theory]
    [InlineData(Case.Nominative, "ein ein eine –")]
    [InlineData(Case.Accusative, "einen ein eine –")]
    [InlineData(Case.Dative, "einem einem einer –")]
    [InlineData(Case.Genitive, "eines eines einer –")]
    public void The_indefinite_article_has_no_plural(Case @case, string row) =>
        AssertRow(c => Determiners.Get("ein").Form(@case, c), row);

    [Theory]
    [InlineData(Case.Nominative, "kein kein keine keine")]
    [InlineData(Case.Accusative, "keinen kein keine keine")]
    [InlineData(Case.Dative, "keinem keinem keiner keinen")]
    [InlineData(Case.Genitive, "keines keines keiner keiner")]
    public void Kein_and_the_possessives_decline_like_ein_with_a_plural(Case @case, string row)
    {
        AssertRow(c => Determiners.Get("kein").Form(@case, c), row);
        AssertRow(c => Determiners.Get("mein").Form(@case, c), row.Replace("kein", "mein"));
    }

    [Fact]
    public void Euer_drops_its_e_before_an_ending() =>
        AssertRow(c => Determiners.Get("euer").Form(Case.Dative, c), "eurem eurem eurer euren");

    [Theory]
    [InlineData(Case.Nominative, "dieser dieses diese diese")]
    [InlineData(Case.Accusative, "diesen dieses diese diese")]
    [InlineData(Case.Dative, "diesem diesem dieser diesen")]
    [InlineData(Case.Genitive, "dieses dieses dieser dieser")]
    public void Dieser_takes_the_endings_of_the_definite_article(Case @case, string row) =>
        AssertRow(c => Determiners.Get("dieser").Form(@case, c), row);

    [Fact]
    public void Plural_only_and_singular_only_determiners_have_no_other_forms()
    {
        AssertRow(c => Determiners.Get("alle").Form(Case.Dative, c), "– – – allen");
        AssertRow(c => Determiners.Get("viele").Form(Case.Genitive, c), "– – – vieler");
        AssertRow(c => Determiners.Get("jeder").Form(Case.Accusative, c), "jeden jedes jede –");
        AssertRow(c => Determiners.Get("drei").Form(Case.Dative, c), "– – – drei");
    }

    [Theory]
    [InlineData(Case.Nominative, "er es e e")]
    [InlineData(Case.Accusative, "en es e e")]
    [InlineData(Case.Dative, "em em er en")]
    [InlineData(Case.Genitive, "en en er er")]
    public void Strong_adjective_endings(Case @case, string row) =>
        AssertRow(c => Adjective.Ending(Inflection.Strong, @case, c), row);

    [Theory]
    [InlineData(Case.Nominative, "e e e en")]
    [InlineData(Case.Accusative, "en e e en")]
    [InlineData(Case.Dative, "en en en en")]
    [InlineData(Case.Genitive, "en en en en")]
    public void Weak_adjective_endings(Case @case, string row) =>
        AssertRow(c => Adjective.Ending(Inflection.Weak, @case, c), row);

    [Theory]
    [InlineData(Case.Nominative, "er es e en")]
    [InlineData(Case.Accusative, "en es e en")]
    [InlineData(Case.Dative, "en en en en")]
    [InlineData(Case.Genitive, "en en en en")]
    public void Mixed_adjective_endings(Case @case, string row) =>
        AssertRow(c => Adjective.Ending(Inflection.Mixed, @case, c), row);

    [Theory]
    [InlineData("der", Inflection.Weak)]
    [InlineData("dieser", Inflection.Weak)]
    [InlineData("alle", Inflection.Weak)]
    [InlineData("beide", Inflection.Weak)]
    [InlineData("ein", Inflection.Mixed)]
    [InlineData("kein", Inflection.Mixed)]
    [InlineData("unser", Inflection.Mixed)]
    [InlineData("viele", Inflection.Strong)]
    [InlineData("einige", Inflection.Strong)]
    [InlineData("mehrere", Inflection.Strong)]
    [InlineData("drei", Inflection.Strong)]
    [InlineData("", Inflection.Strong)]
    public void The_determiner_decides_how_the_adjective_inflects(string lemma, Inflection expected) =>
        Assert.Equal(expected, Determiners.Get(lemma).Adjective);

    [Theory]
    [InlineData("ein", Case.Nominative, GenderNumber.Masculine, "ein guter")]
    [InlineData("der", Case.Dative, GenderNumber.Masculine, "dem guten")]
    [InlineData("alle", Case.Nominative, GenderNumber.Plural, "alle guten")]
    [InlineData("viele", Case.Nominative, GenderNumber.Plural, "viele gute")]
    [InlineData("", Case.Dative, GenderNumber.Masculine, "gutem")]
    [InlineData("mein", Case.Accusative, GenderNumber.Neuter, "mein gutes")]
    public void Determiner_and_adjective_together(string lemma, Case @case, GenderNumber column, string expected)
    {
        var determiner = Determiners.Get(lemma);
        var phrase = $"{determiner.Form(@case, column)} {Adjective.Inflect("gut", determiner.Adjective, @case, column)}";
        Assert.Equal(expected, phrase.Trim());
    }

    [Theory]
    [InlineData(Inflection.Weak, "- - - -", "- - - -")]
    [InlineData(Inflection.Mixed, "x x - -", "- x - -")]
    [InlineData(Inflection.Strong, "x x x x", "x x x x")]
    public void The_adjective_shows_the_signal_where_the_article_does_not(Inflection inflection, string nominative, string accusative)
    {
        string Row(Case @case) => string.Join(' ', Enum.GetValues<GenderNumber>().Select(c => Adjective.ShowsSignal(inflection, @case, c) ? "x" : "-"));
        Assert.Equal(nominative, Row(Case.Nominative));
        Assert.Equal(accusative, Row(Case.Accusative));
        Assert.Equal(inflection == Inflection.Strong ? "- - x x" : "- - - -", Row(Case.Genitive));
    }

    [Theory]
    [InlineData(Case.Nominative, GenderNumber.Masculine, 'r')]
    [InlineData(Case.Accusative, GenderNumber.Masculine, 'n')]
    [InlineData(Case.Dative, GenderNumber.Neuter, 'm')]
    [InlineData(Case.Nominative, GenderNumber.Feminine, 'e')]
    [InlineData(Case.Genitive, GenderNumber.Neuter, 's')]
    public void The_signal_is_the_last_letter_of_the_definite_article(Case @case, GenderNumber column, char signal) =>
        Assert.Equal(signal, Adjective.Signal(@case, column));

    [Theory]
    [InlineData("gut", "gut")]
    [InlineData("hoch", "hoh")]
    [InlineData("dunkel", "dunkl")]
    [InlineData("teuer", "teur")]
    [InlineData("sauer", "saur")]
    [InlineData("bitter", "bitter")]
    [InlineData("leise", "leis")]
    [InlineData("müde", "müd")]
    public void Some_adjectives_change_their_stem_before_an_ending(string adjective, string stem) =>
        Assert.Equal(stem, Adjective.Stem(adjective));
}
