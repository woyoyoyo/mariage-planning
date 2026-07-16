namespace MariagePlanning.Models;

/// <summary>Racine du fichier mariage.json stocké dans le Gist.</summary>
public class WeddingData
{
    public WeddingInfo Wedding { get; set; } = new();
    public List<Person> People { get; set; } = [];
    public List<WeddingDay> Days { get; set; } = [];
}

public class WeddingInfo
{
    public string Name { get; set; } = "";

    /// <summary>Date du Jour J au format "yyyy-MM-dd" (sert au compte à rebours et au badge ✨).</summary>
    public string Date { get; set; } = "";
    public DateTime LastUpdated { get; set; }
}

public class Person
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";

    /// <summary>Couleur hex du badge (ex : "#6366f1").</summary>
    public string Color { get; set; } = "#6366f1";
}

public class WeddingDay
{
    /// <summary>Clé d'identité du jour (unique).</summary>
    public DateOnly Date { get; set; }

    /// <summary>Ex : "J-2 — Préparation".</summary>
    public string Label { get; set; } = "";
    public List<TaskItem> Activities { get; set; } = [];
}

public class TaskItem
{
    public string Id { get; set; } = "";

    /// <summary>Heure "HH:mm", ou null = toute la journée.</summary>
    public string? Time { get; set; }
    public string Title { get; set; } = "";

    /// <summary>Ids des personnes assignées. Vide = tout le monde.</summary>
    public List<string> AssignedTo { get; set; } = [];
    public string? Notes { get; set; }

    /// <summary>todo | done</summary>
    public string Status { get; set; } = "todo";

    public bool IsDone => Status == "done";
}
