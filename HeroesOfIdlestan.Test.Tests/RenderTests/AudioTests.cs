using HeroesOfIdlestan.Core.Model.Meta;
using HeroesOfIdlestan.Core.Render.Audio;

namespace HeroesOfIdlestan.Test.Tests.RenderTests;

public class SoundBankTests
{
    /// <summary>Un son déclaré sans son WAV resterait muet en silence : on le rattrape ici.</summary>
    [Fact]
    public void EverySoundId_HasAReadableWav()
    {
        foreach (var id in Enum.GetValues<SoundId>())
        {
            var wav = SoundBank.Read(id);
            Assert.True(wav != null, $"WAV absent : Resources/sounds/{SoundBank.FileName(id)}");
            Assert.True(WavSample.TryDecode(wav) != null, $"WAV illisible : {SoundBank.FileName(id)}");
        }
    }

    [Fact]
    public void FileName_IsSnakeCase() =>
        Assert.Equal("achievement_unlocked.wav", SoundBank.FileName(SoundId.AchievementUnlocked));
}

public class GameAudioServiceTests
{
    private sealed class RecordingAudio : IAudioService
    {
        public readonly List<SoundId> Loaded = [];
        public readonly List<SoundId> Played = [];
        public readonly List<bool> Suspensions = [];
        public void Load(SoundId id, byte[] wav) => Loaded.Add(id);
        public void Play(SoundId id, float volume) => Played.Add(id);
        public void SetSuspended(bool suspended) => Suspensions.Add(suspended);
        public void Dispose() { }
    }

    [Fact]
    public void WithoutOutput_IsSilentAndDoesNotThrow()
    {
        var audio = new GameAudioService(null);
        audio.OnGameEvent(new GameEvent(0, "producer_bought"));
        Assert.False(audio.IsAvailable);
    }

    [Fact]
    public void LoadsEverySound_AtStartup()
    {
        var output = new RecordingAudio();
        _ = new GameAudioService(output);
        Assert.Equal(Enum.GetValues<SoundId>(), output.Loaded);
    }

    [Fact]
    public void GameEvents_AreMappedToSounds()
    {
        var output = new RecordingAudio();
        var audio = new GameAudioService(output);
        audio.OnGameEvent(new GameEvent(0, "achievement_unlocked", "x"));
        audio.OnGameEvent(new GameEvent(0, "unknown_event"));
        Assert.Equal([SoundId.AchievementUnlocked], output.Played);
    }

    [Fact]
    public void SameSound_IsThrottled()
    {
        var output = new RecordingAudio();
        var audio = new GameAudioService(output);
        for (int i = 0; i < 10; i++) audio.Play(SoundId.AchievementUnlocked);
        Assert.Single(output.Played);
    }

    [Fact]
    public void DisabledOrSuppressed_PlaysNothing()
    {
        var output = new RecordingAudio();
        var audio = new GameAudioService(output) { Enabled = false };
        audio.Play(SoundId.UiConfirm);
        audio.Enabled = true;
        audio.Suppressed = () => true;
        audio.Play(SoundId.UiConfirm);
        Assert.Empty(output.Played);
    }

    [Fact]
    public void Background_SuspendsOutput_OnlyWhenPlayerWantsSilence()
    {
        var output = new RecordingAudio();
        var audio = new GameAudioService(output, playInBackground: false);
        audio.SetHostInBackground(true);
        audio.Play(SoundId.UiConfirm);
        audio.SetHostInBackground(false);

        Assert.Empty(output.Played);
        Assert.Equal([true, false], output.Suspensions);

        var loud = new RecordingAudio();
        new GameAudioService(loud, playInBackground: true).SetHostInBackground(true);
        Assert.Empty(loud.Suspensions);
    }
}
