using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using MariagePlanning.Models;

namespace MariagePlanning.Services;

public record ChatMessage(string Text, bool IsUser, bool IsAction = false);

public class AssistantAction
{
    [JsonPropertyName("action")]    public string       Action     { get; set; } = "";
    [JsonPropertyName("date")]      public string?      Date       { get; set; }  // pour add_task
    [JsonPropertyName("dueDate")]   public string?      DueDate    { get; set; }  // pour add_todo
    [JsonPropertyName("title")]     public string?      Title      { get; set; }
    [JsonPropertyName("time")]      public string?      Time       { get; set; }
    [JsonPropertyName("location")]  public string?      Location   { get; set; }
    [JsonPropertyName("notes")]     public string?      Notes      { get; set; }
    [JsonPropertyName("assignedTo")]public List<string> AssignedTo { get; set; } = [];
    [JsonPropertyName("personIds")] public List<string> PersonIds  { get; set; } = [];
    // add_supply
    [JsonPropertyName("category")]  public string?      Category   { get; set; }
    [JsonPropertyName("status")]    public string?      Status     { get; set; }
    [JsonPropertyName("quantity")]  public int          Quantity   { get; set; } = 1;
    [JsonPropertyName("price")]     public string?      Price      { get; set; }
    [JsonPropertyName("link")]      public string?      Link       { get; set; }
}

public class GeminiService(SettingsService settings)
{
    private static readonly HttpClient _http = new();
    public const string DefaultModel = "gemini-3.1-flash-lite";
    private string ApiUrl => $"https://generativelanguage.googleapis.com/v1beta/models/{settings.GeminiModel}:generateContent";

    public bool IsConfigured => !string.IsNullOrWhiteSpace(settings.GeminiApiKey);

    public async Task<string> ChatAsync(string systemPrompt, IReadOnlyList<ChatMessage> history, string userMessage)
    {
        var key = settings.GeminiApiKey
            ?? throw new InvalidOperationException("Clé Gemini non configurée dans les Paramètres.");

        var contents = history
            .Select(m => (object)new { role = m.IsUser ? "user" : "model", parts = new[] { new { text = m.Text } } })
            .Append(new { role = "user", parts = new[] { new { text = userMessage } } })
            .ToArray();

        var body = new
        {
            system_instruction = new { parts = new[] { new { text = systemPrompt } } },
            contents,
            generationConfig = new { temperature = 0.4, maxOutputTokens = 1024 }
        };

        var resp = await _http.PostAsJsonAsync($"{ApiUrl}?key={key}", body);

        if (!resp.IsSuccessStatusCode)
        {
            if ((int)resp.StatusCode == 503)
                throw new Exception("Le modèle Gemini est momentanément surchargé. Réessaie dans quelques secondes, ou change de modèle dans Paramètres.");

            var err = await resp.Content.ReadAsStringAsync();
            string? message = null;
            try { message = System.Text.Json.JsonDocument.Parse(err).RootElement.GetProperty("error").GetProperty("message").GetString(); } catch { }
            throw new Exception($"Erreur Gemini {(int)resp.StatusCode} — {message ?? err[..Math.Min(200, err.Length)]}");
        }

        var json = await resp.Content.ReadFromJsonAsync<JsonElement>();
        return json.GetProperty("candidates")[0]
                   .GetProperty("content")
                   .GetProperty("parts")[0]
                   .GetProperty("text")
                   .GetString() ?? "";
    }

    /// <summary>
    /// Extrait l'action JSON de la réponse et retourne le texte visible (sans le bloc JSON).
    /// </summary>
    public static (string DisplayText, AssistantAction? Action) ParseResponse(string raw)
    {
        AssistantAction? action = null;

        // Cherche un bloc ```json ... ``` ou ``` ... ```
        var m = Regex.Match(raw, @"```(?:json)?\s*(\{[\s\S]*?\})\s*```");
        if (m.Success)
        {
            action = TryDeserialize(m.Groups[1].Value);
            raw = raw.Remove(m.Index, m.Length).Trim();
        }
        else
        {
            // Cherche un JSON inline { "action": ... }
            var m2 = Regex.Match(raw, @"\{[^{}]*""action""[^{}]*\}");
            if (m2.Success)
            {
                action = TryDeserialize(m2.Value);
                raw = raw.Remove(m2.Index, m2.Length).Trim();
            }
        }

        return (raw.Trim(), action);
    }

    private static AssistantAction? TryDeserialize(string json)
    {
        try { return JsonSerializer.Deserialize<AssistantAction>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }); }
        catch { return null; }
    }

    /// <summary>
    /// Construit le prompt système avec le contexte complet du planning.
    /// </summary>
    public static string BuildSystemPrompt(WeddingData data, string today)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("Tu es un assistant de planification de mariage. Tu réponds TOUJOURS en français, de façon concise et amicale.");
        sb.AppendLine();
        sb.AppendLine($"MARIAGE : {data.Wedding.Name}");
        sb.AppendLine($"Date du mariage : {data.Wedding.Date}");
        sb.AppendLine($"Aujourd'hui : {today}");
        sb.AppendLine();

        sb.AppendLine("PERSONNES (utilise l'ID dans le champ assignedTo du JSON) :");
        foreach (var p in data.People)
            sb.AppendLine($"  - id \"{p.Id}\" → {p.Name}");
        if (data.People.Count == 0) sb.AppendLine("  (aucune personne définie)");
        sb.AppendLine();

        if (data.Teams.Count > 0)
        {
            sb.AppendLine("ÉQUIPES (utilise l'ID d'équipe dans assignedTo, l'app résoudra les membres) :");
            foreach (var t in data.Teams)
            {
                var members = t.MemberIds
                    .Select(mid => data.People.FirstOrDefault(p => p.Id == mid)?.Name ?? mid);
                sb.AppendLine($"  - id \"{t.Id}\" → {t.Name} ({string.Join(", ", members)})");
            }
            sb.AppendLine();
        }

        sb.AppendLine("PLANNING COMPLET :");
        foreach (var day in data.Days.OrderBy(d => d.Date))
        {
            sb.AppendLine($"## {day.Date:yyyy-MM-dd} — {day.Label}");
            if (day.Activities.Count == 0)
            {
                sb.AppendLine("  (aucune tâche)");
            }
            else
            {
                foreach (var t in day.Activities.OrderBy(t => t.Time ?? "99:99"))
                {
                    var time = t.Time is not null ? $"{t.Time} " : "";
                    var people = t.AssignedTo.Count == 0 ? "tout le monde"
                        : string.Join(", ", t.AssignedTo.Select(id =>
                            data.People.FirstOrDefault(p => p.Id == id)?.Name ?? id));
                    var loc = t.Location is not null ? $" 📍{t.Location}" : "";
                    sb.AppendLine($"  • {time}{t.Title} → {people}{loc}");
                    if (t.Notes is not null) sb.AppendLine($"    notes : {t.Notes}");
                }
            }
        }
        sb.AppendLine();

        sb.AppendLine("INSTRUCTIONS :");
        sb.AppendLine("Il existe TROIS types de création :");
        sb.AppendLine();
        sb.AppendLine("1) TÂCHE de planning (sur un jour précis du planning) → action add_task :");
        sb.AppendLine("  ```json");
        sb.AppendLine("  {\"action\":\"add_task\",\"date\":\"YYYY-MM-DD\",\"title\":\"...\",\"time\":\"HH:MM ou null\",\"location\":null,\"notes\":null,\"assignedTo\":[\"id_personne_ou_equipe\"]}");
        sb.AppendLine("  ```");
        sb.AppendLine("  - La date DOIT correspondre à un jour existant dans le planning listé ci-dessus.");
        sb.AppendLine("  - assignedTo peut contenir des IDs de personnes ET/OU des IDs d'équipes.");
        sb.AppendLine("  - Pour 'tout le monde' : assignedTo = []");
        sb.AppendLine();
        sb.AppendLine("2) TODO (élément de liste de choses à faire, avec date butoir) → action add_todo :");
        sb.AppendLine("  ```json");
        sb.AppendLine("  {\"action\":\"add_todo\",\"title\":\"...\",\"dueDate\":\"YYYY-MM-DD ou null\",\"notes\":null,\"personIds\":[\"id_personne\"]}");
        sb.AppendLine("  ```");
        sb.AppendLine("  - personIds contient les IDs de PERSONNES uniquement (pas d'équipes).");
        sb.AppendLine("  - dueDate est la date limite (peut être null si pas de deadline).");
        sb.AppendLine();
        sb.AppendLine("3) MATÉRIEL (article à acheter, commandé ou déjà en stock) → action add_supply :");
        sb.AppendLine("  ```json");
        sb.AppendLine("  {\"action\":\"add_supply\",\"title\":\"...\",\"category\":\"ex: Décoration\",\"status\":\"tobuy\",\"quantity\":1,\"price\":null,\"location\":null,\"notes\":null,\"link\":null}");
        sb.AppendLine("  ```");
        sb.AppendLine("  - status : \"tobuy\" (à acheter), \"ordered\" (commandé), \"have\" (en stock).");
        sb.AppendLine("  - price : prix unitaire en euros sous forme de nombre (ex: 12.50), ou null.");
        sb.AppendLine("  - quantity : nombre entier, défaut 1.");
        sb.AppendLine("  - link : URL d'achat ou null.");
        sb.AppendLine();
        sb.AppendLine("RÈGLES COMMUNES :");
        sb.AppendLine("- Si la demande porte sur un ARTICLE À ACHETER / du matériel / une fourniture → utilise add_supply.");
        sb.AppendLine("- Si la demande porte sur quelque chose à FAIRE/PRÉPARER (pas un objet) sans jour précis → utilise add_todo.");
        sb.AppendLine("- Si la demande porte sur un événement/tâche sur un jour précis du planning → utilise add_task.");
        sb.AppendLine("- Si le titre manque : POSE UNE QUESTION avant d'émettre le JSON.");
        sb.AppendLine("- Réponds d'abord en texte naturel, PUIS ajoute le bloc JSON.");
        sb.AppendLine("- Pour LIRE le planning : réponds en texte naturel, liste les tâches pertinentes.");
        sb.AppendLine("- Tu ne peux pas modifier ni supprimer des éléments existants.");

        return sb.ToString();
    }

    /// <summary>
    /// Résout une liste d'IDs (personnes OU équipes) en liste de person IDs uniques.
    /// </summary>
    public static List<string> ResolveAssignedIds(
        IEnumerable<string> raw,
        IEnumerable<Person> people,
        IEnumerable<Team>   teams)
    {
        var peopleList = people.ToList();
        var teamsList  = teams.ToList();
        var result     = new HashSet<string>();

        foreach (var nameOrId in raw)
        {
            // Cherche une équipe (par ID ou nom)
            var team = teamsList.FirstOrDefault(t => t.Id == nameOrId)
                    ?? teamsList.FirstOrDefault(t => t.Name.Equals(nameOrId, StringComparison.OrdinalIgnoreCase));
            if (team is not null)
            {
                foreach (var mid in team.MemberIds) result.Add(mid);
                continue;
            }

            // Cherche une personne (par ID ou nom)
            var person = peopleList.FirstOrDefault(p => p.Id == nameOrId)
                      ?? peopleList.FirstOrDefault(p => p.Name.Equals(nameOrId, StringComparison.OrdinalIgnoreCase));
            if (person is not null)
                result.Add(person.Id);
        }

        return [.. result];
    }
}
