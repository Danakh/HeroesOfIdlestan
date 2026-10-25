namespace HeroesOfIdlestan.Core.Render.Audio;

/// <summary>
/// Les bruitages du jeu. Chaque valeur correspond au fichier embarqué
/// <c>HeroesOfIdlestan.Core.Render/Resources/sounds/{nom_en_snake_case}.wav</c> — <c>AchievementUnlocked</c> ↔
/// <c>achievement_unlocked.wav</c> (voir <see cref="SoundBank.FileName"/>).
///
/// <para>Jamais sérialisé (couche d'affichage) : une valeur s'ajoute ou se renomme sans toucher aux
/// sauvegardes. Ajouter un son : une valeur ici, son WAV (ou une fonction du même nom dans
/// <c>assets/sounds/generate_sounds.py</c>), et sa règle dans <see cref="GameAudioService"/>.
/// <c>SoundBankTests</c> échoue si un WAV manque.</para>
/// </summary>
public enum SoundId
{
    /// <summary>Retour d'un clic de commande réussi (achat…).</summary>
    UiConfirm,
    /// <summary>Succès débloqué.</summary>
    AchievementUnlocked,
    /// <summary>Combat gagné : court, il revient toutes les quelques secondes.</summary>
    ZoneCleared,
    /// <summary>Toute l'équipe est morte.</summary>
    FightLost,
    /// <summary>Un personnage monte de niveau.</summary>
    LevelUp,
}
