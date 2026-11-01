using HeroesOfIdlestan.Core.Model.Animation;
using HeroesOfIdlestan.Core.Model.State;
using HeroesOfIdlestan.Core.Presentation.Animation;

namespace HeroesOfIdlestan.Core.Render.Animation;

/// <summary>
/// Lecture des planches pour un combattant : une animation de fond (en boucle), par-dessus une
/// animation ponctuelle (intro, attaque, coup reçu) jouée jusqu'au bout, et la mort tenue sur sa
/// dernière image. Pendant de l'<c>AnimationPlayer</c> de <c>FighterView</c> côté Godot, avec les
/// mêmes règles (<see cref="FighterAnimation"/>) et les mêmes secours (<see cref="AnimationResolver"/>).
///
/// <para>En ticks de jeu : les animations suivent la vitesse et s'arrêtent en pause.</para>
/// </summary>
internal sealed class SpriteAnimator
{
    private readonly SpriteSheet _sheet;
    private readonly AnimationResolver _resolver;

    private string? _background;
    private long _backgroundStart;
    private string? _oneShot;
    private long _oneShotStart;
    private string? _death;
    private long _deathStart;
    /// <summary>Image affichée à la mort quand le jeu n'a pas d'animation de mort : la pose se fige.</summary>
    private (SpriteClipData Clip, int Frame)? _frozen;
    private (SpriteClipData Clip, int Frame)? _last;

    public SpriteAnimator(SpriteSheet sheet, AnimationResolver resolver, bool playIntro, long now)
    {
        _sheet = sheet;
        _resolver = resolver;
        if (playIntro && sheet.Data.Has(FighterAnimation.Intro)) (_oneShot, _oneShotStart) = (FighterAnimation.Intro, now);
    }

    /// <summary>Le jeu a sa propre animation de mort : sans elle, la vue fait tomber le corps.</summary>
    public bool HasDeathClip => _resolver.Has(AnimationNames.Death);

    /// <summary>Vrai pendant une animation ponctuelle (intro, attaque, coup reçu).</summary>
    public bool InOneShot => _oneShot != null;

    /// <summary>
    /// Lance l'animation ponctuelle <paramref name="name"/> du catalogue, ou son secours. Rabattue sur
    /// l'attente (ou sur rien), elle n'apporte rien : le fond continue.
    /// </summary>
    public void StartOneShot(string name, long now)
    {
        var clip = _resolver.Resolve(name);
        if (clip == null || clip == _resolver.Resolve(AnimationNames.Idle)) return;
        (_oneShot, _oneShotStart) = (clip, now);
    }

    /// <summary>Mort : joue l'animation de mort une fois et tient sa dernière image, ou fige la pose.</summary>
    public void Die(long now)
    {
        _oneShot = null;
        // Résolue même sans clip, pour que le remplacement figure au journal.
        var clip = _resolver.Resolve(AnimationNames.Death);
        if (HasDeathClip && clip != null) (_death, _deathStart) = (clip, now);
        else _frozen = _last;
    }

    /// <summary>Relevé (retour en place après une défaite) : sa prochaine mort se rejouera.</summary>
    public void Revive() => (_death, _frozen) = (null, null);

    /// <summary>
    /// L'image à dessiner au tick <paramref name="now"/>. <paramref name="background"/> : nom du
    /// catalogue (résolu ici) ou <see cref="FighterAnimation.LookAround"/>. Null si le jeu n'a rien
    /// à montrer.
    /// </summary>
    public (SpriteClipData Clip, int Frame)? Frame(string background, long now)
    {
        if (_death != null && _sheet.Data.Find(_death) is { } death)
            return _last = (death, death.FrameAt(Seconds(now - _deathStart), loop: false));
        if (_frozen != null) return _frozen;

        if (_oneShot != null && _sheet.Data.Find(_oneShot) is { } oneShot)
        {
            double t = Seconds(now - _oneShotStart);
            if (!oneShot.IsFinished(t)) return _last = (oneShot, oneShot.FrameAt(t, loop: false));
        }
        _oneShot = null;

        string? name = background == FighterAnimation.LookAround && _sheet.Data.Has(background)
            ? background
            : _resolver.Resolve(background);
        if (name == null || _sheet.Data.Find(name) is not { } clip) return _last;
        if (!string.Equals(name, _background, StringComparison.Ordinal)) (_background, _backgroundStart) = (name, now);
        return _last = (clip, clip.FrameAt(Seconds(now - _backgroundStart), loop: true));
    }

    private static double Seconds(long ticks) => Math.Max(0, ticks) / (double)GameClock.TicksPerSecond;
}
