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
    [JsonPropertyName("date")]      public string?      Date       { get; set; }
    [JsonPropertyName("title")]     public string?      Title      { get; set; }
    [JsonPropertyName("time")]      public string?      Time       { get; set; }
    [JsonPropertyName("location")]  public string?      Location   { get; set; }
    [JsonPropertyName("notes")]     public string?      Notes      { get; set; }
    [JsonPropertyName("assignedTo")]public List<string> AssignedTo { get; set; } = [];
}

public class GeminiService(SettingsService settings)
{
    private static readonly HttpClient _http = new();
    public const string DefaultModel = "gemini-2.5-flash";
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
            var err = await resp.Content.ReadAsStringAsync();
            throw new Exception($"Erreur Gemini {(int)resp.StatusCode} — {err[..Math.Min(300, err.Length)]}");
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
        sb.AppendLine("- Pour CRÉER une tâche : réponds d'abord en texte naturel, PUIS ajoute un bloc JSON :");
        sb.AppendLine("  ```json");
        sb.AppendLine("  {\"action\":\"add_task\",\"date\":\"YYYY-MM-DD\",\"title\":\"...\",\"time\":\"HH:MM ou null\",\"location\":null,\"notes\":null,\"assignedTo\":[\"id1\"]}");
        sb.AppendLine("  ```");
        sb.AppendLine("- Pour 'tout le monde' : assignedTo = []");
        sb.AppendLine("- Si la date ou le titre manquent : POSE UNE QUESTION avant d'émettre le JSON.");
        sb.AppendLine("- Pour LIRE le planning : réponds en texte naturel, liste les tâches pertinentes.");
        sb.AppendLine("- Tu ne peux pas modifier ni supprimer des tâches existantes.");

        return sb.ToString();
    }

    /// <summary>Résout un nom ou un ID en ID de personne (tolérant aux fautes mineures).</summary>
    public static string? ResolvePersonId(string nameOrId, IEnumerable<Person> people)
    {
        var list = people.ToList();
        var byId = list.FirstOrDefault(p => p.Id == nameOrId);
        if (byId is not null) return byId.Id;
        var byName = list.FirstOrDefault(p => p.Name.Equals(nameOrId, StringComparison.OrdinalIgnoreCase));
        return byName?.Id;
    }
}
