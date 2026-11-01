using HeroesOfIdlestan.Core.Controller.Content;
using HeroesOfIdlestan.Core.Controller.Modifiers;
using HeroesOfIdlestan.Core.Controller.Simulation;
using HeroesOfIdlestan.Core.Model.Entities;
using HeroesOfIdlestan.Core.Model.Meta;
using HeroesOfIdlestan.Core.Model.State;

namespace HeroesOfIdlestan.Core.Controller.Combat;

/// <summary>
/// La boucle courte (GDD §2) : combat automatique → repos → zone suivante. Défaite (toute l'équipe
/// morte) : l'avance automatique se coupe et l'équipe recule d'une zone.
///
/// <para>Simulation par événements : chaque passage traite, dans l'ordre de leurs ticks, toutes les
/// actions et fins de repos échues jusqu'à <see cref="SimulationContext.CurrentTick"/>. Un saut de
/// temps en une seule tranche donne donc exactement la même partie qu'un tick à la fois.</para>
/// </summary>
public sealed class CombatSystem : ISimulationSystem
{
    public string Name => "Combat";

    public void Run(SimulationContext context)
    {
        var state = context.State;
        if (state.Heroes.Count == 0) return;

        // Un combat déjà tranché (sauvegarde éditée à la main) se conclut tout de suite.
        if (state.Run.Phase == RunPhase.Fighting && IsFightOver(state))
            EndFight(state, context.PreviousTick, context.Modifiers);

        while (true)
        {
            if (state.Run.Phase == RunPhase.Resting)
            {
                if (state.Run.RestEndTick > context.CurrentTick) return;
                EndRest(state, state.Run.RestEndTick);
                continue;
            }

            // À égalité de tick, l'arrivée d'un marcheur passe avant les actions.
            var actor = NextActor(state);
            var walker = NextArrival(state);
            if (walker != null && walker.ArrivalTick <= context.CurrentTick
                && (actor == null || walker.ArrivalTick <= actor.NextActionTick))
            {
                Arrive(state, walker);
                continue;
            }
            if (actor == null || actor.NextActionTick > context.CurrentTick) return;
            Act(state, actor, context.Modifiers);
        }
    }

    // ── Combat ───────────────────────────────────────────────────────────

    /// <summary>
    /// Lance le combat de la zone courante au tick <paramref name="tick"/> : le groupe de monstres
    /// apparaît, et chacun frappe pour la première fois au bout de sa cadence.
    /// </summary>
    public static void StartFight(GameState state, long tick)
    {
        state.Monsters.Clear();
        state.Monsters.AddRange(state.Run.Debug ? GameContent.DebugMonsterGroup() : GameContent.MonsterGroup(state.Run.Zone));
        foreach (var monster in state.Monsters)
        {
            var stats = Stats(state, monster);
            monster.Health = stats.MaxHealth;
            monster.NextActionTick = tick + stats.CooldownTicks;
        }
        foreach (var hero in state.Heroes)
        {
            hero.ArrivalTick = 0;
            if (hero.IsAlive) hero.NextActionTick = tick + Stats(state, hero).CooldownTicks;
        }
        // Les groupes du contenu sont des pyramides valides ; une sauvegarde éditée à la main, peut-être pas.
        state.MonsterHoles.Clear();
        Formation.Settle(state.Heroes, state.HeroHoles);
        Formation.Settle(state.Monsters, state.MonsterHoles);

        state.Run.Phase = RunPhase.Fighting;
        state.Run.LastOutcome = FightOutcome.None;
    }

    /// <summary>Statistiques d'un combattant : classe et niveau pour un héros, espèce et zone pour un monstre.</summary>
    public static FighterStats Stats(GameState state, Entity fighter) => fighter switch
    {
        HeroEntity hero => GameContent.HeroStats(hero.Class, hero.Level),
        MonsterEntity monster => GameContent.MonsterStats(monster.Species, state.Run.Zone),
        _ => throw new ArgumentOutOfRangeException(nameof(fighter)),
    };

    /// <summary>Le prochain à agir ; à égalité de tick, les héros d'abord, puis l'ordre des listes.</summary>
    private static Entity? NextActor(GameState state)
    {
        Entity? next = null;
        foreach (var hero in state.Heroes)
            if (hero.IsAlive && (next == null || hero.NextActionTick < next.NextActionTick)) next = hero;
        foreach (var monster in state.Monsters)
            if (monster.IsAlive && (next == null || monster.NextActionTick < next.NextActionTick)) next = monster;
        return next;
    }

    private static void Act(GameState state, Entity actor, ModifierAggregator modifiers)
    {
        long tick = actor.NextActionTick;
        var stats = Stats(state, actor);
        actor.NextActionTick = tick + stats.CooldownTicks;

        bool isHero = actor is HeroEntity;
        IReadOnlyList<Entity> allies = isHero ? state.Heroes : state.Monsters;
        IReadOnlyList<Entity> enemies = isHero ? state.Monsters : state.Heroes;

        var action = actor switch
        {
            HeroEntity hero => GameContent.HeroAction(hero.Class, hero.Line),
            MonsterEntity monster => GameContent.MonsterAction(monster.Species, monster.Line),
            _ => null,
        };
        // Aucune action depuis cette ligne, ou personne à portée : il ne fait rien (GDD §5.1).
        if (action == null) return;
        var target = Formation.ChooseTarget(actor, allies, enemies, action.MinRange, action.MaxRange,
            isHero ? state.HeroHoles : state.MonsterHoles, isHero ? state.MonsterHoles : state.HeroHoles);
        if (target == null) return;

        actor.LastActionTick = tick;
        target.Health = Math.Max(0, target.Health - stats.Damage);
        if (!target.IsAlive) OnDeath(state, target, enemies, tick);

        if (IsFightOver(state)) EndFight(state, tick, modifiers);
    }

    /// <summary>
    /// Une mort, en temps réel (GDD §3.2) : la ligne se resserre, puis ceux de derrière avancent si
    /// la pyramide n'est plus valide. Un monstre tué quitte la liste ; un héros mort y reste.
    /// Celui qui avance marche jusqu'à sa nouvelle place (<see cref="GameContent.MoveTicks"/>) et
    /// n'agit qu'une fois arrivé.
    /// </summary>
    private static void OnDeath(GameState state, Entity fallen, IReadOnlyList<Entity> side, long tick)
    {
        var holes = HolesOf(state, fallen);
        Formation.Fall(side, holes, fallen);
        if (fallen is MonsterEntity monster) state.Monsters.Remove(monster);
        StartWalks(Formation.Settle(side, holes, tick), tick);
    }

    /// <summary>
    /// Arrivée d'un marcheur : il redevient candidat pour avancer, et la ligne qui attendait faute de
    /// candidat libre (<see cref="Formation.Settle"/>) se complète maintenant.
    /// </summary>
    private static void Arrive(GameState state, Entity walker)
    {
        long tick = walker.ArrivalTick;
        walker.ArrivalTick = 0;
        IReadOnlyList<Entity> side = walker is HeroEntity ? state.Heroes : state.Monsters;
        StartWalks(Formation.Settle(side, HolesOf(state, walker), tick), tick);
    }

    /// <summary>Les trous réservés du camp de <paramref name="fighter"/> (héros si null).</summary>
    private static List<FormationHole> HolesOf(GameState state, Entity? fighter) =>
        fighter is MonsterEntity ? state.MonsterHoles : state.HeroHoles;

    /// <summary>Ceux qui avancent marchent jusqu'à leur nouvelle place et n'agissent qu'une fois arrivés.</summary>
    private static void StartWalks(List<Entity> advancers, long tick)
    {
        foreach (var advancer in advancers)
        {
            advancer.ArrivalTick = tick + GameContent.MoveTicks;
            advancer.NextActionTick = Math.Max(advancer.NextActionTick, advancer.ArrivalTick);
        }
    }

    /// <summary>Le prochain marcheur vivant à arriver ; null si personne ne marche.</summary>
    private static Entity? NextArrival(GameState state)
    {
        Entity? next = null;
        foreach (var fighter in state.Heroes.Concat<Entity>(state.Monsters))
            if (fighter.IsAlive && fighter.ArrivalTick > 0 && (next == null || fighter.ArrivalTick < next.ArrivalTick))
                next = fighter;
        return next;
    }

    private static bool IsFightOver(GameState state) =>
        !state.Monsters.Any(m => m.IsAlive) || !state.Heroes.Any(h => h.IsAlive);

    private static void EndFight(GameState state, long tick, ModifierAggregator modifiers)
    {
        var run = state.Run;
        bool victory = state.Heroes.Any(h => h.IsAlive);
        run.Phase = RunPhase.Resting;
        run.RestEndTick = tick + GameContent.RestTicks;
        run.LastOutcome = victory ? FightOutcome.Victory : FightOutcome.Defeat;

        if (!victory)
        {
            run.AutoAdvance = false;
            state.Events.Add(new GameEvent(tick, "fight_lost", run.Zone.ToString()));
            return;
        }

        state.Gold = state.Gold + (long)modifiers.Apply(GameContent.GoldGainModifier, GameContent.GoldReward(run.Zone));
        long xp = (long)modifiers.Apply(GameContent.XpGainModifier, GameContent.XpReward(run.Zone));
        foreach (var hero in state.Heroes)
            if (hero.IsAlive) GainXp(state, hero, xp, tick);
        state.Events.Add(new GameEvent(tick, "fight_won", run.Zone.ToString()));
    }

    /// <summary>
    /// Gain d'XP et montées de niveau. Au niveau maximum, le personnage cesse de progresser pour le
    /// reste du run (GDD §5.4). Les PV gagnés par niveau s'ajoutent aux PV restants.
    /// </summary>
    private static void GainXp(GameState state, HeroEntity hero, long xp, long tick)
    {
        if (hero.Level >= GameContent.MaxLevel) return;
        hero.Xp = hero.Xp + xp;
        while (hero.Level < GameContent.MaxLevel && hero.Xp >= GameContent.XpToNextLevel(hero.Level))
        {
            long before = Stats(state, hero).MaxHealth;
            hero.Xp = hero.Xp - GameContent.XpToNextLevel(hero.Level);
            hero.Level++;
            hero.Health = hero.Health + Stats(state, hero).MaxHealth - before;
            state.Events.Add(new GameEvent(tick, "hero_level_up", hero.Level.ToString()));
        }
        if (hero.Level >= GameContent.MaxLevel) hero.Xp = 0;
    }

    // ── Repos ────────────────────────────────────────────────────────────

    /// <summary>
    /// Fin du repos : soins de base des survivants, puis zone suivante (si l'avance automatique est
    /// active) après une victoire, ou zone précédente après une défaite. Sans résurrection dans la
    /// version de base, une défaite relève toute l'équipe, qui reprend la formation choisie par le
    /// joueur : sinon le run s'arrêterait là. Après une victoire, personne ne recule (GDD §3.2).
    /// </summary>
    private static void EndRest(GameState state, long tick)
    {
        var run = state.Run;
        switch (run.LastOutcome)
        {
            case FightOutcome.Victory:
                if (run.AutoAdvance) run.Zone++;
                break;
            case FightOutcome.Defeat:
                run.Zone = Math.Max(1, run.Zone - 1);
                break;
        }
        run.HighestZone = Math.Max(run.HighestZone, run.Zone);

        foreach (var hero in state.Heroes)
        {
            if (run.LastOutcome == FightOutcome.Defeat)
            {
                hero.Line = hero.FormationLine;
                hero.Rank = 0;
                state.HeroHoles.Clear();
            }
            if (hero.IsAlive || run.LastOutcome == FightOutcome.Defeat)
                hero.Health = Stats(state, hero).MaxHealth;
        }

        StartFight(state, tick);
    }
}
