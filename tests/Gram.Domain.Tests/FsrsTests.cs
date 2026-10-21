namespace Gram.Domain.Tests;

// Expected values come from py-fsrs 6.3.2 itself (Scheduler(learning_steps=(), relearning_steps=(),
// enable_fuzzing=False)), each review on the day the previous one made the card due.
public class FsrsTests
{
    static readonly DateOnly Start = new(2026, 1, 1);

    static List<(int Interval, double Stability, double Difficulty)> Run(params Rating[] ratings)
    {
        var history = new List<(int, double, double)>();
        Memory? memory = null;
        var day = Start;
        foreach (var rating in ratings)
        {
            memory = Fsrs.Review(memory, rating, day);
            history.Add((memory.Due.DayNumber - memory.LastReview.DayNumber, memory.Stability, memory.Difficulty));
            day = memory.Due;
        }
        return history;
    }

    static void AssertMatches((int, double, double)[] expected, List<(int Interval, double Stability, double Difficulty)> actual)
    {
        Assert.Equal(expected.Length, actual.Count);
        for (var i = 0; i < expected.Length; i++)
        {
            var (interval, stability, difficulty) = expected[i];
            Assert.Equal(interval, actual[i].Interval);
            Assert.Equal(stability, actual[i].Stability, 0.0001);
            Assert.Equal(difficulty, actual[i].Difficulty, 0.0001);
        }
    }

    [Fact]
    public void Repeated_right_answers_stretch_the_interval_like_the_reference() => AssertMatches(
        [(2, 2.3065, 2.1181), (11, 10.9643, 2.1112), (46, 46.2802, 2.1043), (163, 162.8622, 2.0975), (497, 497.4472, 2.0906), (1346, 1345.5288, 2.0837)],
        Run(Rating.Good, Rating.Good, Rating.Good, Rating.Good, Rating.Good, Rating.Good));

    [Fact]
    public void Mistakes_and_hesitation_follow_the_reference() => AssertMatches(
        [(1, 0.2120, 6.4133), (2, 1.8868, 6.4021), (5, 4.5223, 7.5968), (11, 11.4450, 7.5844), (1, 1.4461, 9.1912), (2, 2.4531, 9.1773), (4, 4.2398, 9.1633)],
        Run(Rating.Again, Rating.Good, Rating.Hard, Rating.Good, Rating.Again, Rating.Good, Rating.Good));

    [Fact]
    public void A_late_review_uses_the_lower_chance_of_recall()
    {
        var memory = Fsrs.Review(null, Rating.Good, Start);
        memory = Fsrs.Review(memory, Rating.Good, memory.Due);
        var late = memory.LastReview.AddDays(30);

        Assert.Equal(0.8179, Fsrs.Retrievability(memory, late), 0.0001);
        var after = Fsrs.Review(memory, Rating.Good, late);
        Assert.Equal(77, after.Due.DayNumber - after.LastReview.DayNumber);
        Assert.Equal(77.2950, after.Stability, 0.0001);
    }

    [Fact]
    public void A_card_answered_today_is_due_again_on_a_later_day()
    {
        var memory = Fsrs.Review(null, Rating.Again, Start);
        Assert.True(memory.Due > Start);
    }

    [Fact]
    public void Fuzz_keeps_long_intervals_close_to_the_computed_one()
    {
        Memory? memory = null;
        var day = Start;
        for (var i = 0; i < 3; i++) { memory = Fsrs.Review(memory, Rating.Good, day); day = memory.Due; }
        // Unfuzzed, the next interval is 163 days; the fuzz range at 163 is ±10.
        var intervals = Enumerable.Range(0, 200)
            .Select(seed => Fsrs.Review(memory, Rating.Good, day, new Random(seed)))
            .Select(m => m.Due.DayNumber - m.LastReview.DayNumber)
            .ToList();
        Assert.All(intervals, i => Assert.InRange(i, 153, 174));
        Assert.True(intervals.Distinct().Count() > 5);
    }
}
