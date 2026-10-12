using HeroesOfIdlestan.Core.Platform;
using HeroesOfIdlestan.Render.Audio;

namespace HeroesOfIdlestan.Browser.Services;

/// <summary>
/// Persistance web dans le localStorage. Les clés sont préfixées par le jeu : deux jeux servis depuis
/// le même domaine (github.io) partagent le même localStorage.
///
/// <para>Ne jamais changer <see cref="Prefix"/> après une mise en ligne : les joueurs perdraient leur
/// partie. Quota ≈ 5 Mo par domaine — une sauvegarde qui grossit doit rester bien en dessous.</para>
/// </summary>
public sealed class BrowserFileSystemService : IFileSystemService
{
    public const string Prefix = "heroesofidlestan_";

    public void WriteText(string name, string content) => BrowserInterop.SetStorageItem(Prefix + name, content);

    public string? ReadText(string name) => BrowserInterop.GetStorageItem(Prefix + name);

    public void Delete(string name) => BrowserInterop.RemoveStorageItem(Prefix + name);
}

/// <summary>
/// Sortie audio via Web Audio : le navigateur décode, rééchantillonne et mixe lui-même.
///
/// <para><b>Le son ne démarre qu'après un geste du joueur</b> : tout navigateur refuse qu'une page
/// émette avant un clic ou une touche. Le module JS réveille l'AudioContext au premier
/// pointerdown/keydown ; avant, les sons sont ignorés sans erreur.</para>
/// </summary>
public sealed class BrowserAudioService : IAudioService
{
    private bool _disposed;

    public void Load(SoundId id, byte[] wav)
    {
        if (!_disposed) BrowserInterop.AudioLoad(id.ToString(), wav);
    }

    public void Play(SoundId id, float volume)
    {
        if (!_disposed) BrowserInterop.AudioPlay(id.ToString(), volume);
    }

    public void SetSuspended(bool suspended)
    {
        if (!_disposed) BrowserInterop.AudioSetSuspended(suspended);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        BrowserInterop.AudioClose();
    }
}
