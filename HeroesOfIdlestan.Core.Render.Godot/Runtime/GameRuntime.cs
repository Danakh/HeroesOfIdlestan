using Godot;
using HeroesOfIdlestan.Core.Controller.Platform;
using HeroesOfIdlestan.Core.Model.Platform;
using HeroesOfIdlestan.Core.Presentation.Audio;
using HeroesOfIdlestan.Core.Presentation.Runtime;
using HeroesOfIdlestan.Core.Render.Godot.Battle;

namespace HeroesOfIdlestan.Core.Render.Godot.Runtime;

/// <summary>
/// Runtime de la pile Godot : la <see cref="GameSession"/> commune et la scène 3D du combat.
/// Mono-thread : l'hôte (<c>GameRuntimeHost</c>) sérialise tous les appels.
///
/// <para>Tout ce qui ne dessine pas (partie, sauvegarde, sons, commandes) est dans la session : ne
/// rien y ajouter ici qui vaudrait aussi pour Skia.</para>
/// </summary>
public sealed class GameRuntime : IGameRuntime
{
    public GameSession Session { get; }

    /// <summary>La scène 3D : la vue accroche <see cref="BattleScene.Root"/> à son arbre.</summary>
    public BattleScene Scene { get; }

    /// <inheritdoc cref="GameSession(IFileSystemService, IAudioService?, bool, StoreController?, Func{DateTimeOffset}?)"/>
    public GameRuntime(
        IFileSystemService files,
        IAudioService? audio = null,
        bool playSoundInBackground = true,
        StoreController? store = null,
        Func<DateTimeOffset>? now = null)
    {
        Session = new GameSession(files, audio, playSoundInBackground, store, now);
        Scene = new BattleScene(Session.Game);
    }

    /// <summary>Met la scène 3D à jour ; Godot la dessine ensuite sur son propre thread.</summary>
    public void Render(double deltaSeconds) => Scene.Update(Session.Game.State, (float)deltaSeconds);

    /// <summary>Entrée non consommée par le HUD : la caméra. Vrai si l'événement a servi.</summary>
    public bool HandleInput(InputEvent input) => Scene.HandleInput(input);

    public void Dispose()
    {
        Session.Dispose(); // sauvegarde une dernière fois
        Scene.Dispose();
    }
}
