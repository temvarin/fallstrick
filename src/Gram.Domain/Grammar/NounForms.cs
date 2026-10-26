namespace Gram.Domain.Grammar;

/// <summary>
/// The regular written forms of a noun, each with the column of the declension table it stands for. Sentences use them to
/// make a noun tappable and to check a frame's gender; irregular forms are listed in the content by hand (des Kollegen).
/// </summary>
public static class NounForms
{
    public static IEnumerable<(string Form, GenderNumber Column)> Of(string lemma, Gender gender, string? plural)
    {
        var singular = gender.Singular();
        yield return (lemma, singular);
        if (gender != Gender.Feminine)
        {
            // Genitive singular: des Tages, des Kindes.
            yield return (lemma + "s", singular);
            yield return (lemma + "es", singular);
        }
        if (plural is null) yield break;

        yield return (plural, GenderNumber.Plural);
        // n-declension: des / dem / den Nachbarn look like the plural.
        if (gender == Gender.Masculine && (plural == lemma + "n" || plural == lemma + "en")) yield return (plural, singular);
        // Dative plural: den Kindern.
        if (!plural.EndsWith('n') && !plural.EndsWith('s')) yield return (plural + "n", GenderNumber.Plural);
    }
}
