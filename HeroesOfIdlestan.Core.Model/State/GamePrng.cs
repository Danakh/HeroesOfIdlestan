namespace HeroesOfIdlestan.Core.Model.State;

/// <summary>
/// Générateur pseudo-aléatoire seedé et sérialisé avec la partie (Park–Miller, 2^31 − 1).
///
/// <para>Règle : jamais de <c>new GamePrng()</c> ni de <see cref="System.Random"/> dans un système de
/// jeu. Tous les tirages passent par les instances portées par <see cref="GameState"/>, sinon la
/// partie se désynchronise de son seed et le déterminisme (tests, fixtures de sauvegarde) est perdu.</para>
/// </summary>
public sealed class GamePrng
{
    private const uint Modulus = 0x7FFFFFFF;
    private const uint Multiplier = 16807;

    public int Seed { get; set; }

    public uint State { get; set; }

    public GamePrng() : this(42) { }

    public GamePrng(int seed)
    {
        Seed = seed;
        State = BuildState(seed);
    }

    private static uint BuildState(int seed)
    {
        uint s = (uint)seed & Modulus;
        return s == 0 ? 1 : s;
    }

    private double Step()
    {
        if (State == 0) State = BuildState(Seed);
        uint hi = Multiplier * (State >> 16);
        uint lo = Multiplier * (State & 0xFFFF) + ((hi & 0x7FFF) << 16) + (hi >> 15);
        State = lo > Modulus ? lo - Modulus : lo;
        return (double)(State - 1) / Modulus;
    }

    public int Next(int maxExclusive) => maxExclusive <= 1 ? 0 : (int)(Step() * maxExclusive);

    public int Next(int minInclusive, int maxExclusive) => minInclusive + Next(maxExclusive - minInclusive);

    public double NextDouble() => Step();
}
