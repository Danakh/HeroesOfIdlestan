namespace HeroesOfIdlestan.Core.State;

/// <summary>
/// Nombre de cycles complets d'un cooldown écoulés entre deux passages du pipeline.
///
/// <para>Le motif naïf <c>if (now - last &lt; cooldown) return; ... last = now;</c> n'agit qu'une fois
/// par passage, quelle que soit la durée écoulée. En jeu normal cela ne se voit pas ; pendant un saut
/// de temps (une tranche couvre des milliers de ticks), la production est alors sous-simulée.</para>
/// </summary>
public static class TickCooldown
{
    /// <summary>
    /// Renvoie le nombre de cycles écoulés depuis <paramref name="lastActionTick"/> et avance celui-ci
    /// d'autant de cycles (pas jusqu'à <paramref name="now"/> : le reliquat est conservé).
    /// </summary>
    public static long ConsumeElapsedCycles(long now, ref long lastActionTick, long cooldownTicks)
    {
        if (cooldownTicks <= 0) cooldownTicks = 1;
        long elapsed = now - lastActionTick;
        if (elapsed < cooldownTicks) return 0;
        long cycles = elapsed / cooldownTicks;
        lastActionTick += cycles * cooldownTicks;
        return cycles;
    }
}
