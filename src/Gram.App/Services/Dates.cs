namespace Gram.App.Services;

/// <summary>German dates without culture data (the app runs with InvariantGlobalization).</summary>
public static class Dates
{
    private static readonly string[] Weekdays = ["Sonntag", "Montag", "Dienstag", "Mittwoch", "Donnerstag", "Freitag", "Samstag"];
    private static readonly string[] Months =
        ["Januar", "Februar", "März", "April", "Mai", "Juni", "Juli", "August", "September", "Oktober", "November", "Dezember"];

    /// <summary>"Dienstag, 20. Oktober"</summary>
    public static string Full(DateOnly date) => $"{Weekdays[(int)date.DayOfWeek]}, {date.Day}. {Months[date.Month - 1]}";
}
