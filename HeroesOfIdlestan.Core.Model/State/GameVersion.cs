namespace HeroesOfIdlestan.Core.Model.State;

/// <summary>
/// Version du jeu, estampillée dans chaque sauvegarde (<see cref="GameState.SavedGameVersion"/>).
///
/// <para>Changer cette valeur invalide les fixtures <c>Saves/current/</c> : les tests de sauvegarde
/// échouent tant qu'elles ne sont pas régénérées (voir <c>SaveFixtureGeneratorTests</c>). À chaque
/// sortie, <c>Saves/current/</c> est copié en <c>Saves/release-X.Y/</c> puis figé.</para>
/// </summary>
public static class GameVersion
{
    public const string Current = "0.1.0";
}
