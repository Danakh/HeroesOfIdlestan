using System.Text.Json.Serialization;

namespace HeroesOfIdlestan.Core.Model.State;

/// <summary>Où en est la boucle courte : combat, puis repos (GDD §2).</summary>
[JsonConverter(typeof(JsonStringEnumConverter<RunPhase>))]
public enum RunPhase
{
    Fighting,
    Resting,
}

/// <summary>Issue du dernier combat, appliquée à la fin du repos qui le suit.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<FightOutcome>))]
public enum FightOutcome
{
    None,
    Victory,
    Defeat,
}

/// <summary>
/// Progression dans le run en cours : zone, phase de la boucle courte, avance automatique.
/// Un run commence toujours en zone 1 (GDD §2, boucle moyenne).
/// </summary>
public sealed class RunState
{
    /// <summary>Zone en cours, à partir de 1.</summary>
    public int Zone { get; set; } = 1;

    /// <summary>Zone la plus haute atteinte pendant ce run.</summary>
    public int HighestZone { get; set; } = 1;

    /// <summary>Passer à la zone suivante après une victoire ; coupée à chaque défaite (GDD §2).</summary>
    public bool AutoAdvance { get; set; } = true;

    public RunPhase Phase { get; set; } = RunPhase.Resting;

    /// <summary>Fin du repos en cours (tick), quand <see cref="Phase"/> vaut <see cref="RunPhase.Resting"/>.</summary>
    public long RestEndTick { get; set; }

    public FightOutcome LastOutcome { get; set; }

    /// <summary>
    /// Partie de debug : chaque zone oppose le même grand groupe de gobelins
    /// (<c>GameContent.DebugMonsterGroup</c>). Jamais sauvegardée par la session.
    /// </summary>
    public bool Debug { get; set; }
}
