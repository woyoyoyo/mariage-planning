namespace MariagePlanning.Models;

/// <summary>Racine du fichier mariage.json stocké dans le Gist.</summary>
public class WeddingData
{
    public WeddingInfo Wedding { get; set; } = new();
    public List<Person> People { get; set; } = [];
    public List<Team> Teams { get; set; } = [];
    public List<Contact> Contacts { get; set; } = [];
    public List<Venue> Venues { get; set; } = [];
    public List<WeddingDay> Days { get; set; } = [];
    public List<TodoItem> Todos { get; set; } = [];
    public List<SupplyItem> Supplies { get; set; } = [];
}

public class Venue
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string? Address { get; set; }
    public string? Notes { get; set; }
    public List<string> ContactIds { get; set; } = [];
    public List<string> PersonIds { get; set; } = [];
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
    public string Color { get; set; } = "#6366f1";
    public string? Phone { get; set; }
    public string? Address { get; set; }
}

public class Team
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Color { get; set; } = "#94a3b8";
    public List<string> MemberIds { get; set; } = [];
}

public class Contact
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";          // ex : "Fleuriste Dupont"
    public string? ContactPerson { get; set; }      // ex : "Marie"
    public string? Phone { get; set; }
    public string? Address { get; set; }
    public string? Notes { get; set; }
    public List<string> Links { get; set; } = [];
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

    /// <summary>Ids des contacts (prestataires) impliqués dans cette tâche.</summary>
    public List<string> ContactIds { get; set; } = [];

    /// <summary>Adresse libre (lien Google Maps). Utilisé si VenueId est null.</summary>
    public string? Location { get; set; }

    /// <summary>Référence à un lieu prédéfini (WeddingData.Venues).</summary>
    public string? VenueId { get; set; }

    public bool IsDone => Status == "done";
}

public class SupplyItem
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string? Category { get; set; }
    public string Status { get; set; } = "tobuy";  // tobuy | ordered | have
    public int Quantity { get; set; } = 1;
    public string? Location { get; set; }           // ex : "Chez Marie", "Cave"
    public string? VenueId { get; set; }
    public string? Notes { get; set; }
    public string? Link { get; set; }               // lien d'achat
    public decimal? Price { get; set; }             // prix unitaire estimé
}

public class TodoItem
{
    public string Id { get; set; } = "";
    public string Title { get; set; } = "";
    public DateOnly? DueDate { get; set; }
    public string? Notes { get; set; }

    /// <summary>todo | done</summary>
    public string Status { get; set; } = "todo";

    /// <summary>Ids des personnes responsables.</summary>
    public List<string> PersonIds { get; set; } = [];

    /// <summary>Ids des contacts (prestataires) liés.</summary>
    public List<string> ContactIds { get; set; } = [];

    public bool IsDone => Status == "done";
}
