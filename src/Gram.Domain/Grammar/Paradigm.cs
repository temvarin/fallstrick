namespace Gram.Domain.Grammar;

/// <summary>
/// A declension table to show the learner: cases down, masculine / neuter / feminine / plural across. A cell is null where
/// the form doesn't exist (ein in the plural). Ending tables also say where the adjective shows the signal.
/// </summary>
/// <remarks>The app shows tables only through this type; computing forms is the engine's job (<see cref="Determiners"/>, <see cref="Adjective"/>).</remarks>
public sealed class Paradigm
{
    readonly Func<Case, GenderNumber, string?> _cell;
    readonly Func<Case, GenderNumber, bool> _signal;

    Paradigm(Func<Case, GenderNumber, string?> cell, Func<Case, GenderNumber, bool>? signal = null)
    {
        _cell = cell;
        _signal = signal ?? ((_, _) => false);
    }

    public string? this[Case @case, GenderNumber column] => _cell(@case, column);

    /// <summary>Whether the adjective itself shows the signal in this cell; always false for article tables.</summary>
    public bool ShowsSignal(Case @case, GenderNumber column) => _signal(@case, column);

    /// <summary>The forms of one article word: der, den, dem, des …</summary>
    public static Paradigm Of(Determiner determiner) => new(determiner.Form);

    public static Paradigm Definite { get; } = Of(Determiners.Get("der"));

    /// <summary>The indefinite article; ein has no plural, so kein shows the endings there.</summary>
    public static Paradigm Indefinite { get; } =
        new((@case, column) => Determiners.Get(column == GenderNumber.Plural ? "kein" : "ein").Form(@case, column));

    /// <summary>The adjective endings after one kind of determiner, with the cells where the adjective shows the signal.</summary>
    public static Paradigm Endings(Inflection inflection) => new(
        (@case, column) => Adjective.Ending(inflection, @case, column),
        (@case, column) => Adjective.ShowsSignal(inflection, @case, column));
}
