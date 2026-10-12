using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace HeroesOfIdlestan.Core.Obfuscation;

/// <summary>Variante 64 bits d'<see cref="ObfInt"/>.</summary>
[JsonConverter(typeof(ObfLongJsonConverter))]
public readonly struct ObfLong : IEquatable<ObfLong>, IComparable<ObfLong>
{
    private readonly long _a;
    private readonly long _b;

    private ObfLong(long a, long b) { _a = a; _b = b; }

    public long Value
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => unchecked((long)((ulong)(_a + _b) * ValueScrambler.KeyInverse64));
    }

    public static ObfLong From(long value)
    {
        long scrambled = unchecked((long)((ulong)value * ValueScrambler.Key64));
        long a = ValueScrambler.NextNoise64();
        return new ObfLong(a, unchecked(scrambled - a));
    }

    public static implicit operator long(ObfLong value) => value.Value;
    public static implicit operator ObfLong(long value) => From(value);

    public bool Equals(ObfLong other) => Value == other.Value;
    public override bool Equals(object? obj) => obj is ObfLong o && Equals(o);
    public override int GetHashCode() => Value.GetHashCode();
    public int CompareTo(ObfLong other) => Value.CompareTo(other.Value);
    public override string ToString() => Value.ToString(CultureInfo.InvariantCulture);
}

public sealed class ObfLongJsonConverter : JsonConverter<ObfLong>
{
    public override ObfLong Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => ObfLong.From(reader.GetInt64());

    public override void Write(Utf8JsonWriter writer, ObfLong value, JsonSerializerOptions options)
        => writer.WriteNumberValue(value.Value);
}
