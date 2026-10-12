using System.Text.Json.Serialization;

namespace HeroesOfIdlestan.Core.Modifiers;

[JsonConverter(typeof(JsonStringEnumConverter<ModifierType>))]
public enum ModifierType
{
    /// <summary>Ajouté à la base.</summary>
    Additive,
    /// <summary>Multiplie (base + additifs). 0,1 = +10 %.</summary>
    Multiplicative,
    /// <summary>Remplace le résultat ; le dernier enregistré l'emporte.</summary>
    Replacer,
}

/// <summary>
/// Un effet de gameplay. <paramref name="Category"/> est une clé libre (ex. <c>"production"</c>) ;
/// <paramref name="SubCategory"/> précise la cible (un type d'entité, un bâtiment…) quand il y en a une.
/// </summary>
public sealed record Modifier(string Category, ModifierType Type, double Value, string? SubCategory = null);

/// <summary>Source de modificateurs : recherche, prestige, objet équipé, bonus temporaire…</summary>
public interface IModifierProvider
{
    IEnumerable<Modifier> GetModifiers();

    /// <summary>À lever quand <see cref="GetModifiers"/> change de résultat.</summary>
    event Action? Changed;
}

/// <summary>Fournisseur à liste fixe (constantes de race, de difficulté…).</summary>
public sealed class StaticModifierProvider(IEnumerable<Modifier> modifiers) : IModifierProvider
{
    private readonly List<Modifier> _modifiers = modifiers.ToList();
    public IEnumerable<Modifier> GetModifiers() => _modifiers;
    public event Action? Changed { add { } remove { } }
}

/// <summary>
/// Agrège les fournisseurs et calcule une valeur finale : <c>(base + Σ additifs) × Π (1 + multiplicatifs)</c>,
/// sauf remplacement. Résultats mis en cache, invalidés quand un fournisseur change.
///
/// <para>Runtime uniquement : jamais sérialisé, reconstruit par <c>GameController.Initialize</c>.</para>
/// </summary>
public sealed class ModifierAggregator
{
    private readonly List<IModifierProvider> _providers = [];
    private readonly Dictionary<(string, string?), (double Add, double Mul, double? Replace)> _cache = [];

    /// <summary>Incrémenté à chaque invalidation : un appelant détecte un changement par simple comparaison.</summary>
    public int Version { get; private set; }

    public event Action? Changed;

    public IReadOnlyList<IModifierProvider> Providers => _providers;

    /// <summary>Idempotent pour une même instance : un double enregistrement compterait tout deux fois.</summary>
    public void Register(IModifierProvider provider)
    {
        if (_providers.Contains(provider)) return;
        _providers.Add(provider);
        provider.Changed += Invalidate;
        Invalidate();
    }

    public void Unregister(IModifierProvider provider)
    {
        if (!_providers.Remove(provider)) return;
        provider.Changed -= Invalidate;
        Invalidate();
    }

    public void Invalidate()
    {
        _cache.Clear();
        Version++;
        Changed?.Invoke();
    }

    public double Apply(string category, double baseValue, string? subCategory = null)
    {
        var key = (category, subCategory);
        if (!_cache.TryGetValue(key, out var agg))
        {
            agg = Aggregate(category, subCategory);
            _cache[key] = agg;
        }
        return agg.Replace ?? (baseValue + agg.Add) * agg.Mul;
    }

    private (double Add, double Mul, double? Replace) Aggregate(string category, string? subCategory)
    {
        double add = 0, mul = 1;
        double? replace = null;
        foreach (var provider in _providers)
        {
            foreach (var m in provider.GetModifiers())
            {
                if (m.Category != category || m.SubCategory != subCategory) continue;
                switch (m.Type)
                {
                    case ModifierType.Additive: add += m.Value; break;
                    case ModifierType.Multiplicative: mul *= 1 + m.Value; break;
                    case ModifierType.Replacer: replace = m.Value; break;
                }
            }
        }
        return (add, mul, replace);
    }
}
