using System.Text.Json.Nodes;

namespace HeroesOfIdlestan.Core.Model.Persistence;

/// <summary>
/// Transformation du JSON brut d'une sauvegarde, appliquée avant sa désérialisation : renommage de
/// propriété ou de valeur d'enum, champ déplacé, valeur par défaut d'un champ ajouté…
///
/// <para>Une migration s'applique aux sauvegardes estampillées d'une version strictement inférieure à
/// <see cref="IntroducedIn"/> (et à celles sans version). Elle ne se retire jamais : une sauvegarde
/// d'une version très ancienne passe par toute la chaîne, dans l'ordre de la liste.</para>
/// </summary>
public interface ISaveMigration
{
    /// <summary>Version qui a rendu la migration nécessaire, ex. <c>"0.2.0"</c>.</summary>
    string IntroducedIn { get; }

    void Apply(JsonObject root);
}
