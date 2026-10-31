using System.Diagnostics;
using HeroesOfIdlestan.Core.Controller.Platform;
using HeroesOfIdlestan.Core.Model.Platform;
using HeroesOfIdlestan.Core.Presentation.Audio;
using HeroesOfIdlestan.Core.Presentation.Runtime;
using HeroesOfIdlestan.Core.Render.Screens;
using SkiaSharp;

namespace HeroesOfIdlestan.Core.Render.Runtime;

/// <summary>
/// Runtime de la pile Skia : la <see cref="GameSession"/> commune et l'écran courant, qui dessine sur
/// un canevas Skia. Mono-thread : l'hôte (<c>GameRuntimeHost</c>) sérialise tous les appels.
///
/// <para>Tout ce qui ne dessine pas (partie, sauvegarde, sons, commandes) est dans la session : ne
/// rien y ajouter ici qui vaudrait aussi pour Godot.</para>
/// </summary>
public sealed class GameRuntime : IGameRuntime
{
    private readonly Stopwatch _frameTimer = Stopwatch.StartNew();
    private float _totalTime;

    public GameSession Session { get; }
    public IScreen Screen { get; }

    /// <inheritdoc cref="GameSession(IFileSystemService, IAudioService?, bool, StoreController?, Func{DateTimeOffset}?)"/>
    public GameRuntime(
        IFileSystemService files,
        IAudioService? audio = null,
        bool playSoundInBackground = true,
        StoreController? store = null,
        Func<DateTimeOffset>? now = null)
    {
        Session = new GameSession(files, audio, playSoundInBackground, store, now);
        Screen = new GameScreen(Session.Game);
    }

    public void Render(SKCanvas canvas, SKSize size)
    {
        float dt = (float)_frameTimer.Elapsed.TotalSeconds;
        _frameTimer.Restart();
        _totalTime += dt;
        canvas.Clear(new SKColor(0x16, 0x1A, 0x20));
        Screen.Render(canvas, size, dt, _totalTime);
    }

    public void Dispose()
    {
        Session.Dispose(); // sauvegarde une dernière fois
        Screen.Dispose();
    }
}
