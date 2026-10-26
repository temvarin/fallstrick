namespace Gram.Domain;

/// <summary>
/// One practice session: the cards due today plus some new ones, mixed across areas. A card answered
/// wrongly comes back a few cards later until it is answered right; only its first answer counts for the
/// scheduler, so practising it again in the same session doesn't pretend it was remembered.
/// </summary>
public sealed class Session
{
    public const int Size = 20;
    internal const int ComeBackAfter = 3;

    readonly List<Card> _queue;
    readonly HashSet<string> _scheduled = [];
    readonly Progress _progress;
    readonly DateOnly _today;
    readonly Random _random;

    Session(List<Card> queue, Progress progress, DateOnly today, Random random)
    {
        (_queue, _progress, _today, _random) = (queue, progress, today, random);
        Total = queue.Count;
    }

    public Card? Current => _queue.Count > 0 ? _queue[0] : null;

    /// <summary>The number of different cards in this session.</summary>
    public int Total { get; }

    /// <summary>The number of different cards answered at least once.</summary>
    public int Answered => _scheduled.Count;

    /// <summary>The number of different cards whose first answer in this session was right at the first try.</summary>
    public int RightFirstTime { get; private set; }

    public bool IsFinished => _queue.Count == 0;

    /// <param name="cards">All cards of the catalog, in the order new ones should be introduced.</param>
    public static Session Start(IReadOnlyList<Card> cards, Progress progress, DateOnly today, Random random)
    {
        var due = cards.Where(c => progress.IsDue(c.Key, today)).OrderBy(_ => random.Next()).Take(Size).ToList();
        var fresh = NewCards(cards, progress, Math.Min(progress.NewLeft(today), Size - due.Count));
        return new Session(Interleave([.. due, .. fresh]), progress, today, random);
    }

    public void Answer(Rating rating)
    {
        var card = _queue[0];
        _queue.RemoveAt(0);
        if (_scheduled.Add(card.Key))
        {
            _progress.Record(card.Key, rating, _today, _random);
            if (rating == Rating.Good) RightFirstTime++;
        }
        if (rating == Rating.Again) _queue.Insert(Math.Min(ComeBackAfter, _queue.Count), card);
    }

    /// <summary>New cards taken from each area in turn, so no area waits until another one is used up. Confusable cards come as a group.</summary>
    internal static List<Card> NewCards(IReadOnlyList<Card> cards, Progress progress, int count)
    {
        var byArea = cards.Where(c => progress.IsNew(c.Key)).GroupBy(c => c.Area).Select(g => new Queue<Card>(g)).ToList();
        var taken = new List<Card>();
        while (taken.Count < count && byArea.Any(q => q.Count > 0))
        {
            foreach (var queue in byArea.Where(q => q.Count > 0))
            {
                if (taken.Count >= count) break;
                var card = queue.Dequeue();
                if (taken.Contains(card)) continue;
                taken.Add(card);
                if (card.Group is not null)
                    taken.AddRange(queue.Where(c => c.Group == card.Group).Except(taken).ToList());
            }
        }
        return taken;
    }

    /// <summary>
    /// Orders cards so that neighbours come from different areas where possible and confusable cards are never
    /// neighbours. Each step takes the area with the most cards left, which spreads every area evenly.
    /// </summary>
    internal static List<Card> Interleave(List<Card> cards)
    {
        var rest = new List<Card>(cards);
        var result = new List<Card>(cards.Count);
        while (rest.Count > 0)
        {
            var previous = result.Count > 0 ? result[^1] : null;
            var candidates = rest.Where(c => !Confusable(c, previous)).ToList();
            var different = candidates.Where(c => c.Area != previous?.Area).ToList();
            var pool = different.Count > 0 ? different : candidates.Count > 0 ? candidates : rest;
            var next = pool.OrderByDescending(c => rest.Count(r => r.Area == c.Area)).First();
            result.Add(next);
            rest.Remove(next);
        }
        return result;
    }

    static bool Confusable(Card card, Card? other) => other?.Group is not null && card.Group == other.Group;
}
