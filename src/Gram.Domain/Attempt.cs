namespace Gram.Domain;

/// <summary>
/// One card being answered, graded automatically: right at the first try is <see cref="Rating.Good"/>, right after the hint
/// <see cref="Rating.Hard"/>, wrong (or given up) <see cref="Rating.Again"/>. Exercises with a small answer space allow no
/// second try (see <see cref="Exercise.AllowsRetry"/>).
/// </summary>
public sealed class Attempt(Exercise exercise)
{
    readonly List<string> _wrong = [];

    public Exercise Exercise { get; } = exercise;

    /// <summary>The wrong answers given so far, in order.</summary>
    public IReadOnlyList<string> Wrong => _wrong;

    /// <summary>The nudge after a wrong first answer; stays set once given.</summary>
    public string? Hint { get; private set; }

    /// <summary>The grade, once the card is done; null while it is still being answered.</summary>
    public Rating? Rating { get; private set; }

    public bool IsDone => Rating is not null;

    /// <summary>Whether the learner has been given the hint and is answering a second time.</summary>
    public bool AwaitsRetry => Hint is not null && !IsDone;

    /// <returns>The grade if this answer finished the card; null if a second try follows.</returns>
    public Rating? Submit(string answer)
    {
        EnsureOpen();
        if (Exercise.IsRight(answer)) return Rating = AwaitsRetry ? Domain.Rating.Hard : Domain.Rating.Good;

        _wrong.Add(answer);
        if (Hint is null && Exercise.AllowsRetry)
        {
            Hint = Exercise.Hint(answer) ?? "";
            return null;
        }
        return Rating = Domain.Rating.Again;
    }

    /// <summary>The learner asks for the answer instead.</summary>
    public Rating GiveUp()
    {
        EnsureOpen();
        Rating = Domain.Rating.Again;
        return Domain.Rating.Again;
    }

    void EnsureOpen()
    {
        if (IsDone) throw new InvalidOperationException("The card has already been answered.");
    }
}
