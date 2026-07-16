using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace MariagePlanning.Services;

/// <summary>
/// Lecture/écriture du mariage.json stocké dans un Gist GitHub.
/// Lecture via l'API (et non l'URL Raw) pour éviter le cache CDN qui peut
/// resservir une version périmée juste après une sauvegarde.
/// </summary>
public class GistService(HttpClient http)
{
    private const string ApiBase = "https://api.github.com/gists/";
    private const string PreferredFileName = "mariage.json";

    /// <summary>Nom du fichier réellement lu dans le Gist (peut être gistfile1.txt).</summary>
    private string? _lastFileName;

    public static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    /// <summary>Contenu JSON brut du Gist (utile pour le cache localStorage).</summary>
    public async Task<string?> LoadContentAsync(string gistId, string? token = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, ApiBase + gistId);
        AddHeaders(request, token);

        using var response = await http.SendAsync(request);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync();
        using var doc = await JsonDocument.ParseAsync(stream);

        return ExtractFileContent(doc.RootElement);
    }

    /// <summary>
    /// Sauvegarde le contenu dans le Gist (PATCH). Nécessite le token.
    /// Si le fichier lu ne s'appelait pas mariage.json, il est renommé au passage.
    /// </summary>
    public async Task SaveContentAsync(string gistId, string token, string content)
    {
        var sourceName = _lastFileName ?? PreferredFileName;

        var payload = new Dictionary<string, object>
        {
            ["files"] = new Dictionary<string, object>
            {
                [sourceName] = sourceName == PreferredFileName
                    ? new { content }
                    : new { filename = PreferredFileName, content }
            }
        };

        using var request = new HttpRequestMessage(HttpMethod.Patch, ApiBase + gistId)
        {
            Content = JsonContent.Create(payload)
        };
        AddHeaders(request, token);

        using var response = await http.SendAsync(request);
        response.EnsureSuccessStatusCode();

        _lastFileName = PreferredFileName;
    }

    private static void AddHeaders(HttpRequestMessage request, string? token)
    {
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        if (!string.IsNullOrWhiteSpace(token))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

    /// <summary>
    /// Prend le contenu de mariage.json s'il existe, sinon du premier fichier du Gist.
    /// </summary>
    private string? ExtractFileContent(JsonElement root)
    {
        if (!root.TryGetProperty("files", out var files))
            return null;

        string? fallback = null;
        string? fallbackName = null;
        foreach (var file in files.EnumerateObject())
        {
            var content = file.Value.TryGetProperty("content", out var c) ? c.GetString() : null;
            if (file.Name.Equals(PreferredFileName, StringComparison.OrdinalIgnoreCase))
            {
                _lastFileName = file.Name;
                return content;
            }
            if (fallback is null)
            {
                fallback = content;
                fallbackName = file.Name;
            }
        }

        _lastFileName = fallbackName;
        return fallback;
    }
}
