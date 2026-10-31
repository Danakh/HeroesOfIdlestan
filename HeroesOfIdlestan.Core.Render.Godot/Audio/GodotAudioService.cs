using Godot;
using HeroesOfIdlestan.Core.Model.Diagnostics;
using HeroesOfIdlestan.Core.Presentation.Audio;

namespace HeroesOfIdlestan.Core.Render.Godot.Audio;

/// <summary>
/// Sortie audio par le serveur audio de Godot : un <see cref="AudioStreamPlayer"/> par son, polyphone
/// pour qu'un bruitage puisse se superposer à lui-même. Les lecteurs vivent sous le nœud que le head
/// fournit : hors de l'arbre, ils ne sonnent pas.
///
/// <para><b>Rien ne peut faire échouer le jeu.</b> Un WAV illisible est journalisé et ce son reste
/// muet.</para>
/// </summary>
public sealed class GodotAudioService(Node parent) : IAudioService
{
    /// <summary>Voix simultanées d'un même son ; au-delà, Godot coupe la plus ancienne.</summary>
    private const int MaxVoices = 4;

    private readonly Dictionary<SoundId, AudioStreamPlayer> _players = [];
    private bool _suspended;
    private bool _disposed;

    public void Load(SoundId id, byte[] wav)
    {
        if (_disposed) return;
        var stream = AudioStreamWav.LoadFromBuffer(wav);
        if (stream == null)
        {
            GameLog.Error(nameof(GodotAudioService), nameof(Load), $"WAV illisible : {SoundBank.FileName(id)}");
            return;
        }
        if (_players.Remove(id, out var previous)) previous.QueueFree();
        var player = new AudioStreamPlayer { Name = id.ToString(), Stream = stream, MaxPolyphony = MaxVoices };
        parent.AddChild(player);
        _players[id] = player;
    }

    public void Play(SoundId id, float volume)
    {
        if (_disposed || _suspended || !_players.TryGetValue(id, out var player)) return;
        player.VolumeLinear = volume;
        player.Play();
    }

    /// <summary>En arrière-plan, les voix en cours s'arrêtent et les suivantes ne partent pas.</summary>
    public void SetSuspended(bool suspended)
    {
        _suspended = suspended;
        if (!suspended || _disposed) return;
        foreach (var player in _players.Values) player.Stop();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        foreach (var player in _players.Values)
            if (GodotObject.IsInstanceValid(player)) player.QueueFree();
        _players.Clear();
    }
}
