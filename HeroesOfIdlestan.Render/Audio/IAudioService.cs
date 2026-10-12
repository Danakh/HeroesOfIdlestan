namespace HeroesOfIdlestan.Render.Audio;

/// <summary>
/// Sortie audio du head. La couche Render dit <i>quoi</i> jouer et <i>quand</i>
/// (<see cref="GameAudioService"/>) ; chaque head dit <i>comment</i> — miniaudio sur le bureau et
/// Android (<c>DesktopAudioService</c>), Web Audio dans le navigateur (<c>BrowserAudioService</c>).
///
/// <para>Le contrat passe le <b>WAV brut</b> : le navigateur le donne tel quel à
/// <c>decodeAudioData</c>, le bureau le décode lui-même (<see cref="WavSample"/>).</para>
///
/// <para>Un head sans son ne fournit rien : <see cref="GameAudioService"/> accepte null et reste muet
/// (tests, outils). Aucune méthode ne doit lever : un son absent rend le jeu silencieux, il ne
/// doit jamais l'empêcher de tourner.</para>
/// </summary>
public interface IAudioService : IDisposable
{
    /// <summary>Charge (ou remplace) l'échantillon d'un son. Appelé une fois par son au démarrage.</summary>
    void Load(SoundId id, byte[] wav);

    /// <summary>
    /// Joue un son déjà chargé, sans attendre la fin du précédent. <paramref name="volume"/> (0 à 1)
    /// intègre déjà le volume du joueur. Appelé depuis le thread de jeu, sous le verrou du runtime :
    /// ne doit jamais attendre.
    /// </summary>
    void Play(SoundId id, float volume);

    /// <summary>Met la sortie en veille (jeu en arrière-plan) ou la relance. Défaut : rien à faire.</summary>
    void SetSuspended(bool suspended) { }
}
