using System.Text.Json;
using Gram.Domain;

namespace Gram.Pipeline.Tests;

public class PipelineTests
{
    static readonly Dictionary<string, string> Sources = new() { ["duden"] = "Duden" };

    static SourceData Data(
        SourceNoun[]? nouns = null, SourceRektion[]? rektion = null, SourceFrame[]? frames = null,
        SourceWhoWhom[]? whoWhom = null, SourceTerm[]? glossary = null) =>
        new(Sources, nouns ?? [], rektion ?? [], frames ?? [], whoWhom ?? [], [], glossary ?? []);

    static SourceNoun Noun(string de = "Teil", string g = "m", string? hint = null) =>
        new(de, g, "Teile", hint, "часть", "A1", ["duden"]);

    static SourceRektion Rektion(string hint = "Es geht um Bestandteile.", params string[] sentences) =>
        new("bestehen-aus", "bestehen", "aus", "dat", "sich zusammensetzen aus", hint, "состоять из", "B1",
            sentences.Length > 0 ? sentences : ["Die Wohnung besteht {aus} drei Zimmern."], ["duden"]);

    [Fact]
    public void Valid_sources_have_no_problems() =>
        Assert.Empty(Validation.Check(Data([Noun()], [Rektion()], [new("Ich helfe {} Mann.", "der", "dat", "m", "helfen + Dativ")])));

    [Theory]
    [InlineData("Die Wohnung besteht aus drei Zimmern.")]
    [InlineData("Die Wohnung {besteht} {aus} drei Zimmern.")]
    [InlineData("Die Wohnung besteht {} drei Zimmern.")]
    public void A_Rektion_sentence_needs_exactly_one_filled_gap(string sentence) =>
        Assert.Contains(Validation.Check(Data(rektion: [Rektion(sentences: sentence)])), p => p.Contains("needs exactly one"));

    [Fact]
    public void An_answer_must_start_with_the_preposition_or_its_contraction()
    {
        Assert.Empty(Validation.Check(Data(rektion: [Rektion(sentences: "Das hängt {vom|von dem} Wetter ab.") with { Prep = "von", Id = "x" }])));
        Assert.Contains(Validation.Check(Data(rektion: [Rektion(sentences: "Die Wohnung besteht {in} drei Zimmern.")])),
            p => p.Contains("doesn't start with \"aus\""));
    }

    [Fact]
    public void A_hint_must_not_give_away_the_preposition() =>
        Assert.Contains(Validation.Check(Data(rektion: [Rektion(hint: "Es geht um das, woraus etwas ist – aus Teilen.")])),
            p => p.Contains("gives away"));

    [Fact]
    public void Nouns_with_two_genders_each_need_a_hint_that_does_not_name_them()
    {
        Assert.Contains(Validation.Check(Data([Noun(g: "m", hint: "ein Stück"), Noun(g: "n")])), p => p.Contains("each needs a hint"));
        Assert.Contains(Validation.Check(Data([Noun(hint: "das Ersatzteil")])), p => p.Contains("contains the noun itself"));
    }

    [Theory]
    [InlineData("ein", "nom", "pl", "has no form")]
    [InlineData("dieser", "nom", "m", "uses der, ein or kein")]
    [InlineData("jemand", "nom", "m", "unknown determiner")]
    public void Frames_must_have_a_form_for_their_cell(string det, string @case, string g, string problem) =>
        Assert.Contains(Validation.Check(Data(frames: [new("{} Kinder spielen.", det, @case, g, "Subjekt")])), p => p.Contains(problem));

    [Theory]
    [InlineData("Ich helfe {} Mann.", "dat", "m", "helfen + Akkusativ", "doesn't end with Dativ")]
    [InlineData("Ich gehe mit {} Teil.", "akk", "m", "mit + Akkusativ", "mit always takes the Dativ")]
    [InlineData("Ich sehe {} Teile.", "akk", "m", "sehen + Akkusativ", "\"Teile\" is Plural, not maskulin")]
    [InlineData("Ich sehe {} Teil.", "akk", "f", "sehen + Akkusativ", "\"Teil\" is maskulin, not feminin")]
    public void A_frame_must_agree_with_its_reason_and_its_noun(string text, string @case, string g, string why, string problem) =>
        Assert.Contains(Validation.Check(Data([Noun()], frames: [new(text, "der", @case, g, why)])), p => p.Contains(problem));

    [Fact]
    public void A_masculine_noun_of_the_n_declension_may_look_like_its_plural() =>
        Assert.Empty(Validation.Check(Data([new("Nachbar", "m", "Nachbarn", null, "сосед", "A2", ["duden"])],
            frames: [new("Das ist das Auto {} Nachbarn.", "der", "gen", "m", "wessen? → Genitiv")])));

    [Fact]
    public void A_preposition_with_a_fixed_case_must_keep_it() =>
        Assert.Contains(Validation.Check(Data(rektion: [Rektion() with { Case = "akk" }])), p => p.Contains("aus always takes the Dativ"));

    [Fact]
    public void Unknown_sources_are_reported() =>
        Assert.Contains(Validation.Check(Data([Noun() with { Src = ["wikipedia"] }])), p => p.Contains("unknown source"));

    [Fact]
    public void Words_in_sentences_become_tappable_terms()
    {
        var data = DataBuilder.Build(Data(
            [Noun("Zimmer", "n") with { Pl = "Zimmer", Ru = "комната" }],
            [Rektion()],
            glossary: [new SourceTerm("bestehen", ["besteht"], "состоять")]));

        var sentence = data.Rektion[0].Sentences[0];
        Assert.Equal(["aus"], sentence.Answers);
        var tapped = sentence.Before.Concat(sentence.After).Where(s => s.Term is not null).Select(s => (s.Text, data.Terms[s.Term!.Value].Ru));
        Assert.Equal([("besteht", "состоять"), ("Zimmern", "комната")], tapped);
    }

    [Fact]
    public void A_spelling_with_two_genders_shows_both_when_tapped()
    {
        var data = DataBuilder.Build(Data(
            [Noun(g: "m", hint: "ein Stück vom Ganzen"), Noun(g: "n", hint: "ein Gegenstand") with { Ru = "деталь" }],
            whoWhom: [new("x", "Das Teil ist kaputt.", "Wer?", ["a", "b"], 0, "")]));

        Assert.Equal(new Term("der Teil, die Teile / das Teil, die Teile", "часть / деталь"), Assert.Single(data.Terms));
    }

    [Fact]
    public void The_app_can_read_what_the_pipeline_writes()
    {
        var data = DataBuilder.Build(Data([Noun()], [Rektion()], [new("Ich helfe {} Mann.", "der", "dat", "m", "helfen + Dativ", "alt")]));

        var read = JsonSerializer.Deserialize(Json.Write(data), DataJson.Default.CatalogData)!;

        Assert.Equal(data.Nouns, read.Nouns);
        Assert.Equal("aus", read.Rektion[0].Sentences[0].Answers[0]);
        Assert.Equal("alt", read.Frames[0].Adjective);
        Assert.Equal(3, read.ToCatalog().Cards.Count);
    }

    static readonly HashSet<string> Released = ["artikel/Teil/m", "rektion/bestehen-aus"];

    [Fact]
    public void A_released_card_key_may_not_just_disappear()
    {
        var problems = KeyLock.Problems(Released, new Dictionary<string, string?>(), new HashSet<string> { "artikel/Teil/m" });

        Assert.Contains("card key \"rektion/bestehen-aus\" was released", Assert.Single(problems));
    }

    [Fact]
    public void A_gone_key_is_moved_to_its_new_card_or_dropped_on_purpose()
    {
        HashSet<string> current = ["artikel/Teil/m", "rektion/bestehen-aus-dat"];

        Assert.Empty(KeyLock.Problems(Released, new Dictionary<string, string?> { ["rektion/bestehen-aus"] = "rektion/bestehen-aus-dat" }, current));
        Assert.Empty(KeyLock.Problems(Released, new Dictionary<string, string?> { ["rektion/bestehen-aus"] = null }, current));
        Assert.Contains(KeyLock.Problems(Released, new Dictionary<string, string?> { ["rektion/bestehen-aus"] = "rektion/x" }, current),
            p => p.Contains("which is no card"));
        Assert.Contains(KeyLock.Problems(Released, new Dictionary<string, string?> { ["artikel/Teil/m"] = null }, current),
            p => p.Contains("is still a card"));
    }

    [Fact]
    public void The_lock_keeps_every_key_ever_released_sorted()
    {
        var text = KeyLock.Write(["rektion/b"], ["artikel/a", "rektion/b"]);

        Assert.Equal(["artikel/a", "rektion/b"], KeyLock.Read(text).Order(StringComparer.Ordinal));
        Assert.EndsWith("artikel/a\nrektion/b\n", text, StringComparison.Ordinal);
        Assert.Equal(KeyLock.Read(text), KeyLock.Read(text.ReplaceLineEndings("\r\n")));
    }

    [Fact]
    public void Moves_that_carry_progress_over_reach_the_app()
    {
        var data = DataBuilder.Build(Data([Noun()]) with
        {
            KeyMoves = new Dictionary<string, string?> { ["artikel/Teile/m"] = "artikel/Teil/m", ["rektion/weg"] = null },
        });

        var read = JsonSerializer.Deserialize(Json.Write(data), DataJson.Default.CatalogData)!;

        Assert.Equal("artikel/Teil/m", Assert.Single(read.ToCatalog().KeyMoves).Value);
    }

    [Fact]
    public void The_repository_content_is_valid()
    {
        var paths = Paths.FindFrom(AppContext.BaseDirectory);
        Assert.Empty(Validation.Check(SourceData.Load(paths)));
    }
}
