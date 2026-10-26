namespace Gram.Domain.Tests;

public class SessionTests
{
    static readonly DateOnly Today = new(2026, 10, 20);

    static CardKey Key(string key) => new(key);
    static Card Article(string key, string? group = null) => new(Key($"artikel/{key}"), Area.Articles, group);
    static Card Rektion(string key, string? group = null) => new(Key($"rektion/{key}"), Area.Rektion, group);
    static Card Ending(string key) => new(Key($"adjektiv/{key}"), Area.AdjectiveEndings);

    static Progress Learned(DateOnly due, params Card[] cards)
    {
        var progress = new Progress();
        var day = due.AddDays(-1);
        foreach (var card in cards) progress.Record(card.Key, Rating.Again, day, null); // due the next day
        return progress;
    }

    [Fact]
    public void A_session_holds_the_due_cards_and_fills_up_with_new_ones()
    {
        Card[] old = [Article("a"), Article("b")];
        Card[] cards = [.. old, Article("c"), Rektion("r"), Ending("e")];
        var progress = Learned(Today, old);
        progress.SetNewPerDay(2);

        var session = Session.Start(cards, progress, Today, new Random(1));

        Assert.Equal(4, session.Total);
    }

    [Fact]
    public void New_cards_come_from_every_area_in_turn()
    {
        Card[] cards = [Article("a"), Article("b"), Article("c"), Rektion("r1"), Rektion("r2"), Ending("e")];

        var fresh = Session.NewCards(cards, new Progress(), 3);

        Assert.Equal(["artikel/a", "rektion/r1", "adjektiv/e"], fresh.Select(c => c.Key.Value));
    }

    [Fact]
    public void Confusable_cards_are_introduced_together()
    {
        Card[] cards = [Article("Teil/m", "Teil"), Article("See/m", "See"), Article("Teil/n", "Teil")];

        var fresh = Session.NewCards(cards, new Progress(), 1);

        Assert.Equal(["artikel/Teil/m", "artikel/Teil/n"], fresh.Select(c => c.Key.Value));
    }

    [Fact]
    public void Neighbours_come_from_different_areas_where_possible()
    {
        List<Card> cards = [Article("a"), Article("b"), Article("c"), Rektion("r1"), Rektion("r2"), Ending("e")];

        var order = Session.Interleave(cards);

        for (var i = 1; i < order.Count; i++) Assert.NotEqual(order[i - 1].Area, order[i].Area);
    }

    [Fact]
    public void Confusable_cards_are_never_neighbours_even_in_one_area()
    {
        List<Card> cards = [Rektion("aus", "bestehen"), Rektion("auf", "bestehen"), Rektion("in", "bestehen"), Rektion("x"), Rektion("y")];

        var order = Session.Interleave(cards);

        for (var i = 1; i < order.Count; i++)
            Assert.False(order[i - 1].Group == "bestehen" && order[i].Group == "bestehen", string.Join(", ", order.Select(c => c.Key.Value)));
    }

    [Fact]
    public void A_wrong_answer_brings_the_card_back_a_few_cards_later_until_it_is_right()
    {
        Card[] cards = [Article("a"), Rektion("r"), Ending("e"), Article("b"), Rektion("s")];
        var session = Session.Start(cards, new Progress(), Today, new Random(1));
        var first = session.Current!;

        session.Answer(Rating.Again);
        for (var i = 0; i < Session.ComeBackAfter; i++) session.Answer(Rating.Good);

        Assert.Equal(first, session.Current);
        session.Answer(Rating.Good);
        session.Answer(Rating.Good);
        Assert.True(session.IsFinished);
        Assert.Equal(5, session.Answered);
    }

    [Fact]
    public void Only_the_first_answer_of_a_card_counts_for_the_scheduler()
    {
        var card = Article("a");
        var progress = new Progress();
        var session = Session.Start([card], progress, Today, new Random(1));

        session.Answer(Rating.Again);
        session.Answer(Rating.Good); // the same card again, now right

        Assert.True(session.IsFinished);
        Assert.True(progress.IsDue(card.Key, Today.AddDays(1)));
    }

    [Fact]
    public void Right_first_time_counts_cards_whose_first_answer_was_good()
    {
        Card[] cards = [Article("a"), Rektion("r"), Ending("e")];
        var session = Session.Start(cards, new Progress(), Today, new Random(1));

        session.Answer(Rating.Good);
        session.Answer(Rating.Hard);
        session.Answer(Rating.Again);
        session.Answer(Rating.Good); // the failed card again

        Assert.True(session.IsFinished);
        Assert.Equal(1, session.RightFirstTime);
    }

    [Fact]
    public void Starting_a_card_uses_up_the_new_cards_of_the_day()
    {
        var progress = new Progress();
        progress.SetNewPerDay(2);
        progress.Record(Key("artikel/a"), Rating.Good, Today, null);

        Assert.Equal(1, progress.NewLeft(Today));
        Assert.Equal(2, progress.NewLeft(Today.AddDays(1)));
    }

    [Fact]
    public void Progress_survives_a_save_and_load()
    {
        var store = new MemoryStore();
        var progress = new Progress();
        progress.SetNewPerDay(7);
        progress.Record(Key("artikel/a"), Rating.Good, Today, null);

        new ProgressStore(store).Save(progress);
        var loaded = new ProgressStore(store).Load();

        Assert.Equal(7, loaded.NewPerDay);
        Assert.False(loaded.IsNew(Key("artikel/a")));
        Assert.True(loaded.IsDue(Key("artikel/a"), Today.AddDays(2)));
        Assert.Equal(6, loaded.NewLeft(Today));
    }

    [Fact]
    public void Unreadable_progress_starts_fresh()
    {
        var store = new MemoryStore();
        store.Write("gram.progress", "{not json");

        Assert.True(new ProgressStore(store).Load().IsNew(Key("artikel/a")));
    }

    [Fact]
    public void A_backup_restores_the_progress_and_other_files_are_refused()
    {
        var progress = new Progress();
        progress.Record(Key("artikel/a"), Rating.Good, Today, null);

        var restored = ProgressStore.TryRead(ProgressStore.Backup(progress));

        Assert.False(restored!.IsNew(Key("artikel/a")));
        Assert.Null(ProgressStore.TryRead("""{"schema":1,"data":{}}"""));
        Assert.Null(ProgressStore.TryRead("not json"));
    }

    [Fact]
    public void The_backup_reminder_comes_after_thirty_days_once_there_is_progress()
    {
        var progress = new Progress();
        Assert.False(progress.NeedsBackup(Today));

        progress.Record(Key("artikel/a"), Rating.Good, Today, null);
        Assert.True(progress.NeedsBackup(Today));

        progress.MarkBackedUp(Today);
        Assert.False(progress.NeedsBackup(Today.AddDays(29)));
        Assert.True(progress.NeedsBackup(Today.AddDays(30)));
    }

    sealed class MemoryStore : IKeyValueStore
    {
        readonly Dictionary<string, string> _values = [];
        public string? Read(string key) => _values.GetValueOrDefault(key);
        public void Write(string key, string value) => _values[key] = value;
    }
}
