namespace Gram.Domain;

/// <summary>How well an answer went. The app grades automatically: right first time, right after the hint, wrong.</summary>
public enum Rating { Again = 1, Hard = 2, Good = 3 }

/// <summary>What the scheduler knows about one card.</summary>
/// <param name="Stability">Days until the chance of recalling the card falls to 90 %.</param>
/// <param name="Difficulty">1 (easy) to 10 (hard).</param>
public sealed record Memory(double Stability, double Difficulty, DateOnly LastReview, DateOnly Due);

/// <summary>
/// FSRS-6 with its default parameters and 90 % desired retention, ported from py-fsrs 6.3 (MIT,
/// https://github.com/open-spaced-repetition/py-fsrs). Simplified for a once-a-day practice app:
/// whole days instead of timestamps, and no learning steps, because a card answered wrongly comes back
/// later in the same session instead (see <see cref="Session"/>), and only its first answer of the day counts.
/// </summary>
public static class Fsrs
{
    static readonly double[] W =
    [
        0.212, 1.2931, 2.3065, 8.2956, 6.4133, 0.8334, 3.0194, 0.001, 1.8722, 0.1666, 0.796,
        1.4835, 0.0614, 0.2629, 1.6483, 0.6014, 1.8729, 0.5425, 0.0912, 0.0658, 0.1542,
    ];

    const double DesiredRetention = 0.9;
    const int MaximumInterval = 36500;
    const double MinStability = 0.001;
    static readonly double Decay = -W[20];
    static readonly double Factor = Math.Pow(0.9, 1 / Decay) - 1;

    /// <summary>The chance of recalling the card on <paramref name="day"/>.</summary>
    public static double Retrievability(Memory memory, DateOnly day) =>
        Math.Pow(1 + Factor * Math.Max(0, day.DayNumber - memory.LastReview.DayNumber) / memory.Stability, Decay);

    /// <param name="fuzz">Spreads longer intervals by a few days so cards learned together don't stay due together; null in tests.</param>
    public static Memory Review(Memory? memory, Rating rating, DateOnly today, Random? fuzz = null)
    {
        double stability, difficulty;
        if (memory is null)
        {
            stability = Math.Max(W[(int)rating - 1], MinStability);
            difficulty = ClampDifficulty(InitialDifficulty((int)rating));
        }
        else
        {
            var elapsed = today.DayNumber - memory.LastReview.DayNumber;
            stability = elapsed < 1
                ? ShortTermStability(memory.Stability, rating)
                : NextStability(memory, rating, Retrievability(memory, today));
            difficulty = NextDifficulty(memory.Difficulty, rating);
        }

        var interval = NextInterval(stability);
        if (fuzz is not null) interval = Fuzz(interval, fuzz);
        return new Memory(stability, difficulty, today, today.AddDays(interval));
    }

    static double InitialDifficulty(int rating) => W[4] - Math.Exp(W[5] * (rating - 1)) + 1;

    static double ClampDifficulty(double difficulty) => Math.Clamp(difficulty, 1, 10);

    static double NextDifficulty(double difficulty, Rating rating)
    {
        var delta = -W[6] * ((int)rating - 3);
        var damped = difficulty + (10 - difficulty) * delta / 9;
        // Mean reversion towards the initial difficulty of an "Easy" first answer.
        return ClampDifficulty(W[7] * InitialDifficulty(4) + (1 - W[7]) * damped);
    }

    static double ShortTermStability(double stability, Rating rating)
    {
        var increase = Math.Exp(W[17] * ((int)rating - 3 + W[18])) * Math.Pow(stability, -W[19]);
        if (rating != Rating.Again) increase = Math.Max(increase, 1);
        return Math.Max(stability * increase, MinStability);
    }

    static double NextStability(Memory memory, Rating rating, double retrievability)
    {
        var (s, d) = (memory.Stability, memory.Difficulty);
        var next = rating == Rating.Again
            ? Math.Min(
                W[11] * Math.Pow(d, -W[12]) * (Math.Pow(s + 1, W[13]) - 1) * Math.Exp((1 - retrievability) * W[14]),
                s / Math.Exp(W[17] * W[18]))
            : s * (1 + Math.Exp(W[8]) * (11 - d) * Math.Pow(s, -W[9]) * (Math.Exp((1 - retrievability) * W[10]) - 1)
                * (rating == Rating.Hard ? W[15] : 1));
        return Math.Max(next, MinStability);
    }

    static int NextInterval(double stability)
    {
        var days = (int)Math.Round(stability / Factor * (Math.Pow(DesiredRetention, 1 / Decay) - 1), MidpointRounding.ToEven);
        return Math.Clamp(days, 1, MaximumInterval);
    }

    static int Fuzz(int interval, Random random)
    {
        if (interval < 2.5) return interval;
        var delta = 1.0;
        foreach (var (start, end, factor) in new[] { (2.5, 7.0, 0.15), (7.0, 20.0, 0.1), (20.0, double.PositiveInfinity, 0.05) })
            delta += factor * Math.Max(Math.Min(interval, end) - start, 0);
        var max = Math.Min((int)Math.Round(interval + delta, MidpointRounding.ToEven), MaximumInterval);
        var min = Math.Min(Math.Max(2, (int)Math.Round(interval - delta, MidpointRounding.ToEven)), max);
        return Math.Min((int)Math.Round(random.NextDouble() * (max - min + 1) + min, MidpointRounding.ToEven), MaximumInterval);
    }
}
