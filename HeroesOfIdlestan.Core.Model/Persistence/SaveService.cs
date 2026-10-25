using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization.Metadata;
using HeroesOfIdlestan.Core.Model.Entities;
using HeroesOfIdlestan.Core.Model.State;

namespace HeroesOfIdlestan.Core.Model.Persistence;

/// <summary>
/// Sérialisation de <see cref="GameState"/> : estampille de version, migrations, polymorphisme.
///
/// <para><see cref="Serialize"/> est pur à l'estampille près : il ne touche ni à l'horloge ni à
/// <see cref="GameClock.LastSaveTime"/> (c'est la commande de sauvegarde du contrôleur qui la pose).
/// C'est ce qui permet au test de round-trip d'exiger un JSON identique octet pour octet.</para>
/// </summary>
public sealed class SaveService
{
    /// <summary>Migrations du jeu, dans l'ordre. Vide tant qu'aucune version n'a été livrée.</summary>
    public static readonly IReadOnlyList<ISaveMigration> DefaultMigrations = [];

    /// <summary>
    /// Options construites une fois : un <see cref="JsonSerializerOptions"/> reconstruit tout son cache
    /// de métadonnées par réflexion à chaque instanciation.
    /// </summary>
    public static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = false,
        AllowOutOfOrderMetadataProperties = true,
        TypeInfoResolver = new DefaultJsonTypeInfoResolver
        {
            Modifiers = { EntityRegistry.Instance.ApplyTo },
        },
    };

    private readonly IReadOnlyList<ISaveMigration> _migrations;

    public SaveService(IReadOnlyList<ISaveMigration>? migrations = null)
    {
        _migrations = migrations ?? DefaultMigrations;
    }

    /// <summary>JSON en clair de l'état, estampillé de <see cref="GameVersion.Current"/>.</summary>
    public string Serialize(GameState state)
    {
        state.SavedGameVersion = GameVersion.Current;
        return JsonSerializer.Serialize(state, JsonOptions);
    }

    public GameState Deserialize(string json)
    {
        JsonObject root;
        try
        {
            root = JsonNode.Parse(json) as JsonObject
                   ?? throw new SaveCorruptedException("La racine de la sauvegarde n'est pas un objet JSON.");
        }
        catch (JsonException ex)
        {
            throw new SaveCorruptedException("JSON invalide.", ex);
        }

        Migrate(root);

        try
        {
            return root.Deserialize<GameState>(JsonOptions)
                   ?? throw new SaveCorruptedException("Sauvegarde vide.");
        }
        catch (JsonException ex)
        {
            throw new SaveCorruptedException("La sauvegarde ne correspond pas au modèle.", ex);
        }
    }

    public string Export(GameState state) => SaveCodec.Encode(Serialize(state));

    public GameState Import(string content) => Deserialize(SaveCodec.Decode(content));

    private void Migrate(JsonObject root)
    {
        var saved = ParseVersion(root["SavedGameVersion"]?.GetValue<string>());
        foreach (var migration in _migrations)
        {
            if (saved == null || saved < ParseVersion(migration.IntroducedIn))
                migration.Apply(root);
        }
    }

    private static Version? ParseVersion(string? text) => Version.TryParse(text, out var v) ? v : null;
}
