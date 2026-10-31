using HeroesOfIdlestan.Core.Model.Diagnostics;
using HeroesOfIdlestan.Core.Presentation.Audio;
using SoundFlow.Abstracts;
using SoundFlow.Abstracts.Devices;
using SoundFlow.Backends.MiniAudio;
using SoundFlow.Backends.MiniAudio.Devices;
using SoundFlow.Enums;
using SoundFlow.Structs;

namespace HeroesOfIdlestan.App.Desktop.Audio;

/// <summary>
/// Sortie audio native via miniaudio (SoundFlow) : WASAPI sous Windows, CoreAudio sous macOS,
/// ALSA/PulseAudio sous Linux, AAudio/OpenSL ES sous Android. Les binaires natifs voyagent dans le
/// paquet NuGet.
///
/// <para><b>Partagée avec le head Android</b>, qui lie ce fichier (voir son csproj) : rien ici ne doit
/// dépendre du bureau. Elle ne vit pas dans HeroesOfIdlestan.Core.UI parce que le head navigateur, qui en
/// dépend aussi, n'a que faire de SoundFlow.</para>
///
/// <para><b>Le mélange est fait ici</b> plutôt que par les lecteurs de SoundFlow : un bruitage dure
/// moins d'une seconde et doit pouvoir se superposer à lui-même. Additionner des échantillons
/// pré-décodés coûte moins que construire et détruire des nœuds du graphe audio à chaque son.</para>
///
/// <para><b>Rien ne peut faire échouer le jeu.</b> Machine sans carte son, serveur audio absent,
/// pilote qui refuse le format : l'échec est journalisé une fois et le jeu continue en silence.</para>
/// </summary>
public sealed class NativeAudioService : IAudioService
{
    /// <summary>Voix simultanées ; au-delà, la plus ancienne est remplacée.</summary>
    private const int MaxVoices = 12;
    private const int PreferredSampleRate = 44100;
    private const int PreferredChannels = 2;

    private readonly MiniAudioEngine? _engine;
    private readonly AudioPlaybackDevice? _device;
    private readonly SfxMixer? _mixer;

    /// <summary>
    /// Fréquence réellement accordée par le pilote : un périphérique qui impose 48 kHz ferait jouer des
    /// échantillons préparés pour 44,1 kHz presque un demi-ton trop bas, sans erreur.
    /// </summary>
    private readonly int _sampleRate = PreferredSampleRate;
    private bool _disposed;

    public NativeAudioService()
    {
        try
        {
            _engine = new MiniAudioEngine();
            var preferred = new AudioFormat
            {
                SampleRate = PreferredSampleRate,
                Channels = PreferredChannels,
                Format = SampleFormat.F32,
            };
            _device = _engine.InitializePlaybackDevice(null, preferred, new MiniAudioDeviceConfig());
            _sampleRate = _device.Format.SampleRate;
            _mixer = new SfxMixer(_engine, _device.Format);
            _device.MasterMixer.AddComponent(_mixer);
            _device.Start();
        }
        catch (Exception ex)
        {
            GameLog.Error(nameof(NativeAudioService), nameof(NativeAudioService), ex);
            Dispose();
        }
    }

    /// <summary>Faux = jeu silencieux, sans autre conséquence.</summary>
    public bool IsAvailable => _mixer != null && !_disposed;

    public void Load(SoundId id, byte[] wav)
    {
        if (_mixer == null || _disposed) return;
        var sample = WavSample.TryDecode(wav);
        if (sample == null)
        {
            GameLog.Error(nameof(NativeAudioService), nameof(Load), $"WAV illisible : {SoundBank.FileName(id)}");
            return;
        }
        // Rééchantillonné une fois au chargement, pas sur le thread audio à chaque lecture.
        _mixer.Store(id, sample.Resample(_sampleRate).Samples);
    }

    public void Play(SoundId id, float volume) => _mixer?.Trigger(id, volume);

    /// <summary>Arrête le périphérique en arrière-plan : sur Android, un flux ouvert vide la batterie.</summary>
    public void SetSuspended(bool suspended)
    {
        if (_device == null || _disposed) return;
        try
        {
            if (suspended) _device.Stop();
            else _device.Start();
        }
        catch (Exception ex)
        {
            GameLog.Error(nameof(NativeAudioService), nameof(SetSuspended), ex);
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        // Arrêter le périphérique avant de défaire le graphe : le thread de mixage pourrait encore y entrer.
        try { _device?.Stop(); } catch (Exception ex) { GameLog.Error(nameof(NativeAudioService), nameof(Dispose), ex); }
        try { _device?.Dispose(); } catch (Exception ex) { GameLog.Error(nameof(NativeAudioService), nameof(Dispose), ex); }
        try { _engine?.Dispose(); } catch (Exception ex) { GameLog.Error(nameof(NativeAudioService), nameof(Dispose), ex); }
    }

    /// <summary>
    /// Composant du graphe audio qui additionne les voix actives. Le thread de jeu déclenche, le thread
    /// de mixage consomme ; le verrou ne protège que quelques champs.
    /// </summary>
    private sealed class SfxMixer(AudioEngine engine, AudioFormat format) : SoundComponent(engine, format)
    {
        private struct Voice
        {
            public float[]? Samples;
            public int Position;
            public float Gain;
        }

        private readonly Dictionary<SoundId, float[]> _samples = [];
        private readonly Voice[] _voices = new Voice[MaxVoices];
        private readonly Lock _gate = new();
        private int _nextVoice;

        public override string Name { get; set; } = "Bruitages";

        public void Store(SoundId id, float[] samples)
        {
            lock (_gate) _samples[id] = samples;
        }

        /// <summary>
        /// Appelé depuis le thread UI, sous le verrou du runtime : ne doit <b>jamais</b> attendre. Une
        /// attente de verrou contesté sur le thread UI pompe les messages Win32, peut traiter un
        /// WM_PAINT en réentrance et interbloquer avec le thread de rendu. Verrou pris = son perdu.
        /// </summary>
        public void Trigger(SoundId id, float volume)
        {
            if (!_gate.TryEnter()) return;
            try
            {
                if (!_samples.TryGetValue(id, out var samples)) return;
                int slot = -1;
                for (int i = 0; i < _voices.Length; i++)
                    if (_voices[i].Samples == null) { slot = i; break; }
                if (slot < 0)
                {
                    slot = _nextVoice;
                    _nextVoice = (_nextVoice + 1) % _voices.Length;
                }
                _voices[slot] = new Voice { Samples = samples, Position = 0, Gain = Math.Clamp(volume, 0f, 1f) };
            }
            finally
            {
                _gate.Exit();
            }
        }

        protected override void GenerateAudio(Span<float> buffer, int channels)
        {
            buffer.Clear();
            if (channels <= 0) return;
            int frames = buffer.Length / channels;
            lock (_gate)
            {
                for (int v = 0; v < _voices.Length; v++)
                {
                    var samples = _voices[v].Samples;
                    if (samples == null) continue;
                    int position = _voices[v].Position;
                    float gain = _voices[v].Gain;
                    int count = Math.Min(frames, samples.Length - position);
                    for (int f = 0; f < count; f++)
                    {
                        // Bruitages mono : le même échantillon part sur tous les canaux.
                        float value = samples[position + f] * gain;
                        int at = f * channels;
                        for (int c = 0; c < channels; c++) buffer[at + c] += value;
                    }
                    position += count;
                    if (position >= samples.Length) _voices[v] = default;
                    else _voices[v].Position = position;
                }
                for (int i = 0; i < buffer.Length; i++)
                    buffer[i] = Math.Clamp(buffer[i], -1f, 1f);
            }
        }
    }
}
