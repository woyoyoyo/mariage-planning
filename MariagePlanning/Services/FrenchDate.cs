namespace MariagePlanning.Services;

/// <summary>
/// Formatage des dates en français sans dépendre des cultures ICU
/// (le template Blazor WASM est publié en InvariantGlobalization).
/// </summary>
public static class FrenchDate
{
    private static readonly string[] Days =
        ["dimanche", "lundi", "mardi", "mercredi", "jeudi", "vendredi", "samedi"];

    private static readonly string[] Months =
        ["janvier", "février", "mars", "avril", "mai", "juin",
         "juillet", "août", "septembre", "octobre", "novembre", "décembre"];

    /// <summary>Ex : "jeudi 10 juin" / "mardi 1er septembre".</summary>
    public static string Long(DateOnly d) =>
        $"{Days[(int)d.DayOfWeek]} {DayNumber(d)} {Months[d.Month - 1]}";

    /// <summary>Ex : "10 juin – 13 juin".</summary>
    public static string Range(DateOnly start, DateOnly end) =>
        $"{DayNumber(start)} {Months[start.Month - 1]} – {DayNumber(end)} {Months[end.Month - 1]}";

    private static string DayNumber(DateOnly d) => d.Day == 1 ? "1er" : d.Day.ToString();
}
