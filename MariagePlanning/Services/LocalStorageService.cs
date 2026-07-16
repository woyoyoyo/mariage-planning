using Microsoft.JSInterop;

namespace MariagePlanning.Services;

/// <summary>Accès minimaliste au localStorage du navigateur.</summary>
public class LocalStorageService(IJSRuntime js)
{
    public ValueTask<string?> GetAsync(string key) =>
        js.InvokeAsync<string?>("localStorage.getItem", key);

    public ValueTask SetAsync(string key, string value) =>
        js.InvokeVoidAsync("localStorage.setItem", key, value);

    public ValueTask RemoveAsync(string key) =>
        js.InvokeVoidAsync("localStorage.removeItem", key);
}
