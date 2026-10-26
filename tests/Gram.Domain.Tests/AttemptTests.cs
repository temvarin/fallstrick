using Gram.Domain.Grammar;

namespace Gram.Domain.Tests;

public class AttemptTests
{
    static readonly Noun Messer = new("Messer", Gender.Neuter, "Messer", null, "нож", Level.A2);

    static readonly Frame HelfenDem = new(
        [new Segment("Ich helfe ")], [new Segment(" Mann.")], "der", Case.Dative, GenderNumber.Masculine, "helfen + Dativ");

    // der/die/das: no second try.
    static Attempt Article() => new(new ArticleExercise(new Card(Messer.Key, Area.Articles), Messer, null, []));

    // Every form of der: a hint and a second try.
    static Attempt CaseForm() => new(new CaseExercise(new Card(Catalog.KeyOf(HelfenDem), Area.Cases), HelfenDem));

    [Fact]
    public void Right_at_the_first_try_is_good()
    {
        var attempt = Article();

        Assert.Equal(Rating.Good, attempt.Submit("das"));
        Assert.True(attempt.IsDone);
        Assert.Empty(attempt.Wrong);
    }

    [Fact]
    public void Without_a_second_try_a_wrong_answer_is_again()
    {
        var attempt = Article();

        Assert.Equal(Rating.Again, attempt.Submit("der"));
        Assert.Null(attempt.Hint);
        Assert.Equal(["der"], attempt.Wrong);
    }

    [Fact]
    public void A_wrong_first_answer_earns_a_hint_and_right_after_it_is_hard()
    {
        var attempt = CaseForm();

        Assert.Null(attempt.Submit("den"));
        Assert.True(attempt.AwaitsRetry);
        Assert.Contains("helfen + Dativ, maskulin", attempt.Hint);

        Assert.Equal(Rating.Hard, attempt.Submit("dem"));
        Assert.False(attempt.AwaitsRetry);
    }

    [Fact]
    public void Wrong_again_after_the_hint_is_again()
    {
        var attempt = CaseForm();
        attempt.Submit("den");

        Assert.Equal(Rating.Again, attempt.Submit("des"));
        Assert.Equal(["den", "des"], attempt.Wrong);
    }

    [Fact]
    public void Giving_up_is_again_and_ends_the_card()
    {
        var attempt = CaseForm();
        attempt.Submit("den");

        Assert.Equal(Rating.Again, attempt.GiveUp());
        Assert.Throws<InvalidOperationException>(() => attempt.Submit("dem"));
    }
}
