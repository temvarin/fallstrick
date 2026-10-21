using Gram.App.Pages;
using Gram.App.Services;

namespace Gram.App.Tests;

public class PracticeTests : AppTestContext
{
    private IRenderedComponent<WithTopBar> Practice() => Render<WithTopBar>(p => p.AddChildContent<Practice>());

    [Fact]
    public void A_right_article_is_confirmed_and_scheduled_for_a_later_day()
    {
        Use(Catalog([Messer]));
        var page = Practice();

        Choose(page, "das");

        Assert.Equal("Richtig", page.Find(".tag").TextContent);
        Assert.Contains("das Messer, die Messer", page.Find(".feedback").TextContent);
        Assert.False(Progress.IsNew(Messer.Key));
        Assert.False(Progress.IsDue(Messer.Key, Today));
    }

    [Fact]
    public void A_wrong_article_has_no_second_try_and_comes_back_in_the_same_session()
    {
        Use(Catalog([Messer]));
        var page = Practice();

        Choose(page, "der");

        Assert.Equal("Nicht richtig", page.Find(".tag").TextContent);
        page.Find(".btn.next").Click();
        Assert.Contains("Messer", page.Find(".sentence").TextContent);
        Assert.True(Progress.IsDue(Messer.Key, Today.AddDays(1)));
    }

    [Fact]
    public void A_wrong_case_gets_a_hint_and_a_second_try()
    {
        Use(Catalog(frames: [HelfenDem]));
        var page = Practice();

        Choose(page, "den");
        Assert.Contains("helfen + Dativ, maskulin", page.Find(".hint-card").TextContent);

        Choose(page, "dem");
        Assert.Equal("Richtig im zweiten Versuch", page.Find(".tag").TextContent);
        Assert.Contains("Ich helfe dem Mann.", page.Find(".sentence").TextContent);
    }

    [Fact]
    public void A_typed_preposition_is_checked()
    {
        Use(Catalog(rektion: [BestehenAus]));
        var page = Practice();

        page.Find("form.typed").Submit();
        Assert.Equal("Erst etwas eintippen.", page.Find(".error").TextContent);

        page.Find("input").Input(" Aus ");
        page.Find("form.typed").Submit();

        Assert.Equal("Richtig", page.Find(".tag").TextContent);
        Assert.Contains("bestehen aus + Dativ", page.Find(".feedback").TextContent);
    }

    [Fact]
    public void Tapping_a_word_shows_its_translation()
    {
        Use(Catalog(rektion: [BestehenAus]));
        var page = Practice();

        page.Find(".term").Click();

        Assert.Equal("состоять", Assert.IsType<TermModal>(Dialogs.Current).Term.Ru);
    }

    [Fact]
    public void The_end_of_a_session_shows_how_many_were_right_first_time()
    {
        Use(Catalog([Messer], frames: [HelfenDem]));
        var page = Practice();

        for (var i = 0; i < 2; i++)
        {
            Choose(page, page.Find(".sentence").TextContent.Contains("Messer") ? "das" : "dem");
            page.Find(".btn.next").Click();
        }

        Assert.Contains("Geschafft", page.Markup);
        Assert.Contains("2 von 2 Karten beim ersten Versuch richtig", page.Markup);
    }
}
