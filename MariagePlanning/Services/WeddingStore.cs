using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.JSInterop;
using MariagePlanning.Models;

namespace MariagePlanning.Services;

/// <summary>Provenance des données actuellement affichées.</summary>
public enum DataSource
{
    None,
    Demo,
    Gist,
    Cache
}

/// <summary>
/// Source unique des données du mariage pour l'UI.
/// Stratégie : Gist configuré + réseau → API GitHub (puis copie en cache localStorage) ;
/// hors-ligne ou erreur réseau → lecture silencieuse du cache ;
/// rien de configuré → données de démo locales.
/// </summary>
public class WeddingStore(
    HttpClient http,
    GistService gist,
    SettingsService settings,
    LocalStorageService storage,
    IJSRuntime js)
{
    private const string CacheKey = "mp.cache";
    private const string CacheDateKey = "mp.cacheDate";

    public WeddingData? Data { get; private set; }
    public string? LoadError { get; private set; }
    public DataSource Source { get; private set; } = DataSource.None;

    /// <summary>Date de la dernière synchro réussie avec le Gist.</summary>
    public DateTimeOffset? LastSync { get; private set; }

    /// <summary>Déclenché quand Data change (reload ou sauvegarde) — pour rafraîchir l'UI.</summary>
    public event Action? Changed;

    /// <summary>La sauvegarde nécessite : Gist + token configurés (l'état réseau est vérifié au moment de sauver).</summary>
    public bool CanEdit => settings.CanSave && Source != DataSource.Demo;

    public async Task EnsureLoadedAsync()
    {
        if (Data is null)
            await ReloadAsync();
    }

    public async Task ReloadAsync()
    {
        LoadError = null;
        await settings.EnsureLoadedAsync();

        if (!settings.IsConfigured)
        {
            await LoadDemoAsync();
            return;
        }

        if (await IsOnlineAsync())
        {
            try
            {
                var content = await gist.LoadContentAsync(settings.GistId!, settings.Token);
                if (!string.IsNullOrWhiteSpace(content))
                {
                    Data = JsonSerializer.Deserialize<WeddingData>(content, GistService.JsonOpts);
                    Source = DataSource.Gist;
                    LastSync = DateTimeOffset.Now;
                    await storage.SetAsync(CacheKey, content);
                    await storage.SetAsync(CacheDateKey, LastSync.Value.ToString("O"));
                    Changed?.Invoke();
                    return;
                }
                LoadError = "Le Gist est vide — colle le mariage.json dedans.";
            }
            catch (Exception ex)
            {
                LoadError = ex.Message;
            }
        }

        // Hors-ligne ou échec réseau : on lit silencieusement le cache.
        if (await LoadFromCacheAsync())
        {
            Changed?.Invoke();
            return;
        }

        // Pas de cache non plus : dernier recours, la démo (avec l'erreur affichée).
        await LoadDemoAsync();
    }

    /// <summary>
    /// Sauvegarde sûre (fetch-avant-PATCH) : re-télécharge la dernière version du Gist,
    /// applique la modification dessus, puis pousse le tout. Évite d'écraser une
    /// modification faite par un autre téléphone avec une copie locale périmée.
    /// </summary>
    /// <param name="applyChange">Modification à appliquer sur les données fraîches.
    /// Retourne false pour annuler (ex : cible introuvable).</param>
    public async Task<(bool Ok, string? Error)> SaveAsync(Func<WeddingData, bool> applyChange)
    {
        await settings.EnsureLoadedAsync();

        if (!settings.IsConfigured || settings.Token is null)
            return (false, "Token GitHub manquant — configure-le dans Paramètres.");

        if (!await IsOnlineAsync())
            return (false, "Hors ligne — modification impossible sans réseau.");

        try
        {
            var content = await gist.LoadContentAsync(settings.GistId!, settings.Token);
            var fresh = string.IsNullOrWhiteSpace(content)
                ? null
                : JsonSerializer.Deserialize<WeddingData>(content, GistService.JsonOpts);

            if (fresh is null)
                return (false, "Impossible de relire le Gist avant la sauvegarde.");

            if (!applyChange(fresh))
                return (false, "Modification impossible sur la dernière version des données.");

            fresh.Wedding.LastUpdated = DateTime.UtcNow;
            var json = JsonSerializer.Serialize(fresh, GistService.JsonOpts);

            await gist.SaveContentAsync(settings.GistId!, settings.Token, json);

            Data = fresh;
            Source = DataSource.Gist;
            LastSync = DateTimeOffset.Now;
            await storage.SetAsync(CacheKey, json);
            await storage.SetAsync(CacheDateKey, LastSync.Value.ToString("O"));
            Changed?.Invoke();
            return (true, null);
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

    private async Task<bool> LoadFromCacheAsync()
    {
        try
        {
            var cached = await storage.GetAsync(CacheKey);
            if (string.IsNullOrWhiteSpace(cached))
                return false;

            Data = JsonSerializer.Deserialize<WeddingData>(cached, GistService.JsonOpts);
            Source = DataSource.Cache;

            var cachedDate = await storage.GetAsync(CacheDateKey);
            LastSync = DateTimeOffset.TryParse(cachedDate, out var d) ? d : null;
            return Data is not null;
        }
        catch
        {
            return false;
        }
    }

    private async Task LoadDemoAsync()
    {
        try
        {
            Data = await http.GetFromJsonAsync<WeddingData>("sample-data/wedding-demo.json", GistService.JsonOpts);
            Source = DataSource.Demo;
            Changed?.Invoke();
        }
        catch (Exception ex)
        {
            LoadError ??= ex.Message;
        }
    }

    public async Task<bool> IsOnlineAsync()
    {
        try
        {
            return await js.InvokeAsync<bool>("mariage.isOnline");
        }
        catch
        {
            return true; // au bénéfice du doute, on tentera le fetch
        }
    }

    /// <summary>Jours triés par date.</summary>
    public IReadOnlyList<WeddingDay> OrderedDays =>
        Data?.Days.OrderBy(d => d.Date).ToList() ?? [];

    /// <summary>Date du Jour J si elle est renseignée et valide.</summary>
    public DateOnly? WeddingDate =>
        DateOnly.TryParse(Data?.Wedding.Date, out var d) ? d : null;

    /// <summary>true si ce jour est le Jour J (badge ✨, ouvert par défaut).</summary>
    public bool IsWeddingDay(WeddingDay day) => WeddingDate == day.Date;

    public Person? PersonById(string id) =>
        Data?.People.FirstOrDefault(p => p.Id == id);

    public Team? TeamById(string id) =>
        Data?.Teams.FirstOrDefault(t => t.Id == id);

    public Venue? VenueById(string id) =>
        Data?.Venues.FirstOrDefault(v => v.Id == id);

    /// <summary>Personnes assignées à une tâche (vide = tout le monde).</summary>
    public IEnumerable<Person> AssignedPeople(TaskItem task) =>
        task.AssignedTo
            .Select(PersonById)
            .Where(p => p is not null)!;

    /// <summary>true si la tâche concerne cette personne (assignée ou "tout le monde").</summary>
    public static bool ConcernsPerson(TaskItem task, string personId) =>
        task.AssignedTo.Count == 0 || task.AssignedTo.Contains(personId);
}
