namespace HeroesOfIdlestan.Core.State;

/// <summary>
/// Version du jeu, estampillée dans chaque sauvegarde (<see cref="GameState.SavedGameVersion"/>).
///
/// <para>Changer cette valeur invalide les fixtures <c>saves/current/</c> : les tests de sauvegarde
/// échouent tant qu'elles ne sont pas régénérées (voir <c>SaveFixtureGeneratorTests</c>). À chaque
/// sortie, <c>saves/current/</c> est copié en <c>saves/release-X.Y/</c> puis figé.</para>
/// </summary>
public static class GameVersion
{
    public const string Current = "0.1.0";
}
