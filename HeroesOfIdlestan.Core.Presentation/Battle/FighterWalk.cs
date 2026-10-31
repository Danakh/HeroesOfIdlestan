using HeroesOfIdlestan.Core.Controller.Content;
using HeroesOfIdlestan.Core.Model.State;

namespace HeroesOfIdlestan.Core.Presentation.Battle;

/// <summary>
/// La marche d'un combattant vers sa place dans la formation, pour l'affichage : quand sa place
/// change (avancée, resserrement, retour en place après une défaite), il y marche en
/// <see cref="GameContent.MoveTicks"/>, depuis là où il est dessiné à cet instant.
///
/// <para>En ticks de jeu, comme la simulation : la marche s'arrête en pause et suit la vitesse. Les
/// coordonnées sont celles de la pile qui l'utilise (le plan du sol : X et Y en Skia, X et Z en
/// Godot) ; elle ne lit ni ne modifie l'état.</para>
/// </summary>
public sealed class FighterWalk
{
    /// <summary>Pas par seconde de marche, pour les animations qui marquent les pas.</summary>
    public const float StepsPerSecond = 3f;

    private float _fromX, _fromY, _toX, _toY;
    private long _start;
    private bool _placed;

    /// <summary>Vrai une fois une première place donnée par <see cref="Target"/>.</summary>
    public bool IsPlaced => _placed;

    /// <summary>
    /// Donne la place visée au tick <paramref name="now"/>. La première fois, le combattant y est
    /// posé sans marcher ; ensuite, une place différente lance une marche depuis la position affichée.
    /// </summary>
    public void Target(float x, float y, long now)
    {
        if (!_placed)
        {
            (_fromX, _fromY, _toX, _toY) = (x, y, x, y);
            _placed = true;
            return;
        }
        if (x == _toX && y == _toY) return;
        (_fromX, _fromY) = PositionAt(now);
        (_toX, _toY) = (x, y);
        _start = now;
    }

    /// <summary>Arrête la marche là où elle en est (combattant tué en chemin).</summary>
    public void Halt(long now)
    {
        (_fromX, _fromY) = (_toX, _toY) = PositionAt(now);
    }

    /// <summary>Vrai tant que le combattant n'est pas arrivé.</summary>
    public bool IsWalking(long now) => _placed && Progress(now) < 1f;

    /// <summary>Position affichée au tick <paramref name="now"/>.</summary>
    public (float X, float Y) PositionAt(long now)
    {
        float t = Progress(now);
        return (_fromX + (_toX - _fromX) * t, _fromY + (_toY - _fromY) * t);
    }

    /// <summary>Direction de la dernière marche (non normalisée), à lire pendant <see cref="IsWalking"/>.</summary>
    public (float X, float Y) Heading => (_toX - _fromX, _toY - _fromY);

    /// <summary>Phase du pas en cours, de 0 à 1 ; un pas par 1/<see cref="StepsPerSecond"/> seconde.</summary>
    public float StepPhase(long now)
    {
        float seconds = Math.Max(0, now - _start) / (float)GameClock.TicksPerSecond;
        float steps = seconds * StepsPerSecond;
        return steps - MathF.Floor(steps);
    }

    private float Progress(long now)
    {
        if (_fromX == _toX && _fromY == _toY) return 1f;
        return Math.Clamp((now - _start) / (float)GameContent.MoveTicks, 0f, 1f);
    }
}
