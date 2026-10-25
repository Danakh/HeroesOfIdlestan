using System.Runtime.CompilerServices;

namespace HeroesOfIdlestan.Core.Model.Obfuscation;

/// <summary>
/// Ingrédients du brouillage de <see cref="ObfInt"/> / <see cref="ObfLong"/> : une clé multiplicative
/// impaire tirée au lancement (donc inversible modulo 2^n) et un bruit rapide pour découper chaque
/// valeur en deux moitiés.
///
/// <para>Le bruit est volontairement séparé de <c>GamePrng</c> : il ne doit consommer aucun tirage
/// de la partie, sous peine de casser le déterminisme. Rien de ce qu'il produit n'est observable
/// depuis le modèle — seule la somme des deux moitiés l'est.</para>
/// </summary>
internal static class ValueScrambler
{
    internal static readonly uint Key32;
    internal static readonly uint KeyInverse32;
    internal static readonly ulong Key64;
    internal static readonly ulong KeyInverse64;

    static ValueScrambler()
    {
        var g1 = Guid.NewGuid().ToByteArray();
        var g2 = Guid.NewGuid().ToByteArray();
        Key32 = BitConverter.ToUInt32(g1, 0) | 1u;
        Key64 = BitConverter.ToUInt64(g2, 0) | 1ul;
        KeyInverse32 = Inverse32(Key32);
        KeyInverse64 = Inverse64(Key64);
    }

    /// <summary>Inverse modulo 2^32 par itération de Newton (3 → 6 → 12 → 24 → 48 bits justes).</summary>
    private static uint Inverse32(uint k)
    {
        uint inv = k;
        unchecked { for (int i = 0; i < 4; i++) inv *= 2u - k * inv; }
        return inv;
    }

    private static ulong Inverse64(ulong k)
    {
        ulong inv = k;
        unchecked { for (int i = 0; i < 5; i++) inv *= 2ul - k * inv; }
        return inv;
    }

    [ThreadStatic] private static uint _noiseState;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int NextNoise32()
    {
        uint x = _noiseState;
        if (x == 0)
            x = unchecked((uint)(Environment.TickCount64 ^ (Environment.CurrentManagedThreadId * 0x9E3779B9L))) | 1u;
        x ^= x << 13;
        x ^= x >> 17;
        x ^= x << 5;
        _noiseState = x;
        return unchecked((int)x);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static long NextNoise64()
    {
        ulong high = unchecked((uint)NextNoise32());
        ulong low = unchecked((uint)NextNoise32());
        return unchecked((long)((high << 32) | low));
    }
}
