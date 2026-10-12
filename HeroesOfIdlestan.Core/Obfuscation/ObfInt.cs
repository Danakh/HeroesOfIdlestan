using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace HeroesOfIdlestan.Core.Obfuscation;

/// <summary>
/// Entier brouillé en mémoire, pour toute quantité que le joueur lit à l'écran et qu'un éditeur de
/// mémoire voudrait modifier (stock, points…). Transparent pour les appelants grâce aux conversions
/// implicites ; la sauvegarde n'écrit que la valeur réelle.
///
/// <para>Dans les tests, écrire <c>Assert.Equal&lt;int&gt;(5, x)</c> : l'inférence est ambiguë sinon.</para>
/// </summary>
[JsonConverter(typeof(ObfIntJsonConverter))]
public readonly struct ObfInt : IEquatable<ObfInt>, IComparable<ObfInt>
{
    private readonly int _a;
    private readonly int _b;

    private ObfInt(int a, int b) { _a = a; _b = b; }

    public int Value
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => unchecked((int)((uint)(_a + _b) * ValueScrambler.KeyInverse32));
    }

    public static ObfInt From(int value)
    {
        int scrambled = unchecked((int)((uint)value * ValueScrambler.Key32));
        int a = ValueScrambler.NextNoise32();
        return new ObfInt(a, unchecked(scrambled - a));
    }

    public static implicit operator int(ObfInt value) => value.Value;
    public static implicit operator ObfInt(int value) => From(value);

    public bool Equals(ObfInt other) => Value == other.Value;
    public override bool Equals(object? obj) => obj is ObfInt o && Equals(o);
    public override int GetHashCode() => Value.GetHashCode();
    public int CompareTo(ObfInt other) => Value.CompareTo(other.Value);
    public override string ToString() => Value.ToString(CultureInfo.InvariantCulture);
}

public sealed class ObfIntJsonConverter : JsonConverter<ObfInt>
{
    public override ObfInt Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => ObfInt.From(reader.GetInt32());

    public override void Write(Utf8JsonWriter writer, ObfInt value, JsonSerializerOptions options)
        => writer.WriteNumberValue(value.Value);
}
