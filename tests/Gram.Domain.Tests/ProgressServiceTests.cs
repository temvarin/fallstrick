using Gram.Domain.Grammar;

namespace Gram.Domain.Tests;

public class ProgressServiceTests
{
    static readonly DateOnly Today = new(2026, 10, 20);

    static readonly Noun Messer = new("Messer", Gender.Neuter, "Messer", null, "нож", Level.A2);
    static readonly Noun Zeitung = new("Zeitung", Gender.Feminine, "Zeitungen", null, "газета", Level.A1);

    static Catalog Catalog(IReadOnlyDictionary<string, string>? moves = null) => new([Messer, Zeitung], [], [], [], [], [], [], moves);

    // What another start of the app would load.
    static Progress Stored(MemoryStore store) => new ProgressStore(store).Load();

    [Fact]
    public void Every_answer_is_saved_as_it_is_recorded()
    {
        var store = new MemoryStore();
        var session = new ProgressService(new ProgressStore(store), Catalog()).StartSession(Today, new Random(1));

        session.Answer(Rating.Good);

        Assert.Single(new[] { Messer.Key, Zeitung.Key }, key => !Stored(store).IsNew(key));
    }

    [Fact]
    public void Settings_and_the_backup_date_are_saved()
    {
        var store = new MemoryStore();
        var learner = new ProgressService(new ProgressStore(store), Catalog());

        learner.SetNewPerDay(15);
        learner.MarkBackedUp(Today);

        Assert.Equal(15, Stored(store).NewPerDay);
        Assert.Equal(Today, Stored(store).LastBackup);
    }

    [Fact]
    public void A_restored_backup_is_saved_and_moved_to_this_release_s_card_keys()
    {
        var old = new Progress();
        old.Record(new CardKey("artikel/Messer/m"), Rating.Good, Today, null);
        var store = new MemoryStore();
        var learner = new ProgressService(new ProgressStore(store), Catalog(new Dictionary<string, string> { ["artikel/Messer/m"] = Messer.Key.Value }));

        learner.Restore(ProgressService.ReadBackup(ProgressStore.Backup(old))!);

        Assert.False(Stored(store).IsNew(Messer.Key));
        Assert.Null(ProgressService.ReadBackup("not a backup"));
    }

    [Fact]
    public void Reset_forgets_everything_and_saves_that()
    {
        var store = new MemoryStore();
        var learner = new ProgressService(new ProgressStore(store), Catalog());
        learner.StartSession(Today, new Random(1)).Answer(Rating.Good);

        learner.Reset();

        Assert.True(Stored(store).IsNew(Messer.Key) && Stored(store).IsNew(Zeitung.Key));
    }

    [Fact]
    public void The_next_session_size_is_what_a_session_would_hold()
    {
        var learner = new ProgressService(new ProgressStore(new MemoryStore()), Catalog());
        learner.SetNewPerDay(1);

        Assert.Equal(1, learner.NextSessionSize(Today));
        Assert.Equal(1, learner.StartSession(Today, new Random(1)).Total);
    }

    sealed class MemoryStore : IKeyValueStore
    {
        readonly Dictionary<string, string> _values = [];
        public string? Read(string key) => _values.GetValueOrDefault(key);
        public void Write(string key, string value) => _values[key] = value;
    }
}
