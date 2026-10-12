using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace HeroesOfIdlestan.Core.Persistence;

/// <summary>
/// Table unique discriminant → type concret d'une hiérarchie polymorphe. La fabrique
/// (<see cref="Create"/>) et la sérialisation (<see cref="ApplyTo"/>) lisent la même table : un type
/// oublié ici rend illisible toute sauvegarde qui le contient, d'où le test d'invariant qui vérifie
/// que chaque valeur de <typeparamref name="TKey"/> est enregistrée.
///
/// <para>Le discriminant est écrit par nom (<c>"$kind": "Producer"</c>) : renommer une valeur exige
/// une <see cref="ISaveMigration"/>.</para>
/// </summary>
public sealed class PolymorphicRegistry<TKey, TBase>
    where TKey : struct, Enum
    where TBase : class
{
    public const string DiscriminatorPropertyName = "$kind";

    private readonly Dictionary<TKey, (Type Type, Func<TBase> Factory)> _entries = [];

    public PolymorphicRegistry<TKey, TBase> Register<T>(TKey key) where T : TBase, new()
    {
        if (!_entries.TryAdd(key, (typeof(T), () => new T())))
            throw new InvalidOperationException($"{typeof(TBase).Name} : {key} est déjà enregistré.");
        return this;
    }

    public IReadOnlyCollection<TKey> Keys => _entries.Keys;

    public Type TypeOf(TKey key) => _entries[key].Type;

    public TBase Create(TKey key) =>
        _entries.TryGetValue(key, out var entry)
            ? entry.Factory()
            : throw new KeyNotFoundException($"{typeof(TBase).Name} : aucun type enregistré pour {key}.");

    /// <summary>Modificateur de <see cref="DefaultJsonTypeInfoResolver"/> qui déclare les types dérivés.</summary>
    public void ApplyTo(JsonTypeInfo info)
    {
        if (info.Type != typeof(TBase)) return;

        var options = new JsonPolymorphismOptions
        {
            TypeDiscriminatorPropertyName = DiscriminatorPropertyName,
            UnknownDerivedTypeHandling = JsonUnknownDerivedTypeHandling.FailSerialization,
        };
        foreach (var (key, entry) in _entries.OrderBy(e => e.Key.ToString(), StringComparer.Ordinal))
            options.DerivedTypes.Add(new JsonDerivedType(entry.Type, key.ToString()));
        info.PolymorphismOptions = options;
    }
}
