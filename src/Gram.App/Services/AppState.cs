using Gram.Domain;

namespace Gram.App.Services;

/// <summary>The learner's progress for the running app. Components change <see cref="Progress"/>, then call <see cref="Save"/>.</summary>
public sealed class AppState(ProgressStore store)
{
    public Progress Progress { get; private set; } = store.Load();

    public void Save() => store.Save(Progress);

    /// <summary>Replaces the progress, e.g. with one read from a backup file.</summary>
    public void Replace(Progress progress)
    {
        Progress = progress;
        Save();
    }
}

public static class Clock
{
    /// <summary>The learner's calendar day. Never stored: the app may stay open across midnight.</summary>
    public static DateOnly Today(this TimeProvider clock) => DateOnly.FromDateTime(clock.GetLocalNow().DateTime);
}
