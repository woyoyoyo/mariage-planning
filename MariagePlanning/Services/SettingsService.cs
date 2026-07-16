namespace MariagePlanning.Services;

/// <summary>
/// Configuration locale de l'app (par téléphone) : ID du Gist, token GitHub
/// et personne "connectée" (pour la vue Moi).
/// Stockée uniquement dans le localStorage — ne transite jamais ailleurs.
/// </summary>
public class SettingsService(LocalStorageService storage)
{
    private const string GistIdKey = "mp.gistId";
    private const string TokenKey = "mp.token";
    private const string CurrentPersonKey = "mp.currentPerson";
    private const string LockedTokenKey = "mp.lockedToken";

    private bool _loaded;

    public string? GistId { get; private set; }
    public string? Token { get; private set; }
    public string? CurrentPerson { get; private set; }

    /// <summary>Token chiffré reçu via un lien de partage — déverrouillable plus tard avec le mot de passe.</summary>
    public string? LockedToken { get; private set; }

    /// <summary>Le token n'est pas requis pour lire, seulement pour sauvegarder.</summary>
    public bool IsConfigured => !string.IsNullOrWhiteSpace(GistId);
    public bool CanSave => IsConfigured && !string.IsNullOrWhiteSpace(Token);

    public async Task EnsureLoadedAsync()
    {
        if (_loaded)
            return;
        GistId = await storage.GetAsync(GistIdKey);
        Token = await storage.GetAsync(TokenKey);
        CurrentPerson = await storage.GetAsync(CurrentPersonKey);
        LockedToken = await storage.GetAsync(LockedTokenKey);
        _loaded = true;
    }

    public async Task SaveAsync(string? gistId, string? token)
    {
        GistId = string.IsNullOrWhiteSpace(gistId) ? null : gistId.Trim();
        Token = string.IsNullOrWhiteSpace(token) ? null : token.Trim();

        if (GistId is null)
            await storage.RemoveAsync(GistIdKey);
        else
            await storage.SetAsync(GistIdKey, GistId);

        if (Token is null)
            await storage.RemoveAsync(TokenKey);
        else
            await storage.SetAsync(TokenKey, Token);

        _loaded = true;
    }

    public async Task SetLockedTokenAsync(string? blob)
    {
        LockedToken = string.IsNullOrWhiteSpace(blob) ? null : blob;

        if (LockedToken is null)
            await storage.RemoveAsync(LockedTokenKey);
        else
            await storage.SetAsync(LockedTokenKey, LockedToken);
    }

    public async Task SetCurrentPersonAsync(string? personId)
    {
        CurrentPerson = string.IsNullOrWhiteSpace(personId) ? null : personId;

        if (CurrentPerson is null)
            await storage.RemoveAsync(CurrentPersonKey);
        else
            await storage.SetAsync(CurrentPersonKey, CurrentPerson);
    }
}
