using System.Runtime.InteropServices.JavaScript;

namespace HeroesOfIdlestan.App.Browser.Services;

/// <summary>
/// Pont vers les APIs du navigateur qu'Avalonia n'expose pas : stockage local, visibilité de l'onglet,
/// Web Audio. Le module JS est <c>wwwroot/gameInterop.js</c>.
/// </summary>
internal static partial class BrowserInterop
{
    private const string Module = "gameInterop";

    /// <summary>
    /// Charge le module d'interop, <b>avant</b> de démarrer Avalonia : le runtime lit la sauvegarde dès
    /// sa construction. Chemin relatif à dotnet.js, qui vit dans <c>_framework/</c>.
    /// </summary>
    public static Task InitializeAsync() => JSHost.ImportAsync(Module, "../gameInterop.js");

    // ── Stockage local (synchrone : le WASM n'a qu'un thread) ────────────

    [JSImport("getStorageItem", Module)]
    public static partial string? GetStorageItem(string key);

    [JSImport("setStorageItem", Module)]
    public static partial void SetStorageItem(string key, string value);

    [JSImport("removeStorageItem", Module)]
    public static partial void RemoveStorageItem(string key);

    // ── Page ─────────────────────────────────────────────────────────────

    /// <summary>Retire l'écran de chargement une fois le jeu affiché.</summary>
    [JSImport("appReady", Module)]
    public static partial void AppReady();

    /// <summary>Retour de l'onglet au premier plan, avec la durée passée masqué en secondes.</summary>
    [JSImport("registerVisibilityHandler", Module)]
    public static partial void RegisterVisibilityHandler(
        [JSMarshalAs<JSType.Function<JSType.Number>>] Action<double> onVisible);

    /// <summary>Passage de l'onglet en arrière-plan (vrai) ou au premier plan (faux).</summary>
    [JSImport("registerHiddenHandler", Module)]
    public static partial void RegisterHiddenHandler(
        [JSMarshalAs<JSType.Function<JSType.Boolean>>] Action<bool> onChanged);

    // ── Bruitages (Web Audio) ────────────────────────────────────────────

    [JSImport("audioLoad", Module)]
    public static partial void AudioLoad(string name, byte[] wav);

    [JSImport("audioPlay", Module)]
    public static partial void AudioPlay(string name, float volume);

    [JSImport("audioSetSuspended", Module)]
    public static partial void AudioSetSuspended(bool suspended);

    [JSImport("audioClose", Module)]
    public static partial void AudioClose();
}
