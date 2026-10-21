namespace Gram.Domain.Grammar;

public enum Case { Nominative, Accusative, Dative, Genitive }

public enum Gender { Masculine, Neuter, Feminine }

/// <summary>The four columns of every German declension table: three genders in the singular, one plural.</summary>
public enum GenderNumber { Masculine, Neuter, Feminine, Plural }

/// <summary>How an adjective inflects, decided by the determiner in front of it.</summary>
public enum Inflection { Strong, Weak, Mixed }

/// <summary>A declension table: one row per case, one ending per column.</summary>
internal sealed class Table
{
    readonly string[][] _rows;

    /// <param name="rows">Nominative, accusative, dative, genitive; each "m n f pl", with "-" for no ending.</param>
    public Table(params string[] rows) =>
        _rows = rows.Select(r => r.Split(' ').Select(e => e == "-" ? "" : e).ToArray()).ToArray();

    public string this[Case @case, GenderNumber column] => _rows[(int)@case][(int)column];
}
