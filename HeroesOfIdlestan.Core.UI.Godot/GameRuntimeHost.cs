using Godot;
using HeroesOfIdlestan.Core.Presentation.Runtime;
using HeroesOfIdlestan.Core.Render.Godot.Runtime;

namespace HeroesOfIdlestan.Core.UI.Godot;

/// <summary>
/// Hôte du runtime Godot : le verrou et la boucle communs (<see cref="GameHost{TRuntime}"/>), plus la
/// mise à jour de la scène 3D et l'input de Godot. Sous Godot, tout vit sur le thread principal : le
/// verrou ne coûte presque rien et garde le même contrat qu'avec Avalonia.
/// </summary>
public sealed class GameRuntimeHost(GameRuntime runtime) : GameHost<GameRuntime>(runtime)
{
    public void Render(double deltaSeconds) => Invoke(r => r.Render(deltaSeconds));

    public bool HandleInput(InputEvent input) => Read(r => r.HandleInput(input));
}
