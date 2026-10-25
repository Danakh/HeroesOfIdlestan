using System.Reflection;
using System.Xml.Linq;
using System.Text.Json.Serialization;
using HeroesOfIdlestan.Core.Controller;
using HeroesOfIdlestan.Core.Model.Entities;
using HeroesOfIdlestan.Core.Model.Localization;
using HeroesOfIdlestan.Core.Model.State;

namespace HeroesOfIdlestan.Test.Tests.Invariants;

/// <summary>
/// Niveau 5 — les règles d'architecture, transformées en tests. Une règle qui ne vit que dans un
/// commentaire finit par être enfreinte en silence.
/// </summary>
public class InvariantTests
{
    /// <summary>Les deux couches du Core : Model et Controller.</summary>
    private static readonly Assembly[] Core = [typeof(GameState).Assembly, typeof(GameController).Assembly];

    /// <summary>
    /// Tout enum du Core peut finir dans une sauvegarde : il se sérialise par nom. Un entier se
    /// décalerait silencieusement à l'insertion d'une valeur.
    /// </summary>
    [Fact]
    public void EveryCoreEnum_IsSerializedByName()
    {
        var offenders = Core.SelectMany(a => a.GetTypes())
            .Where(t => t.IsEnum && t.IsPublic)
            .Where(t => t.GetCustomAttribute<JsonConverterAttribute>() is not { ConverterType: { } c }
                        || !c.IsGenericType
                        || c.GetGenericTypeDefinition() != typeof(JsonStringEnumConverter<>))
            .Select(t => t.FullName)
            .ToList();

        Assert.True(offenders.Count == 0,
            "Enums sans [JsonConverter(typeof(JsonStringEnumConverter<T>))] : " + string.Join(", ", offenders));
    }

    /// <summary>Un type d'entité non enregistré rend illisible toute sauvegarde qui le contient.</summary>
    [Fact]
    public void EveryEntityKind_IsRegistered_WithMatchingType()
    {
        foreach (var kind in Enum.GetValues<EntityKind>())
        {
            Assert.Contains(kind, EntityRegistry.Instance.Keys);
            Assert.Equal(kind, EntityRegistry.Instance.Create(kind).Kind);
        }
    }

    /// <summary>
    /// L'ordre de la simulation est un choix de conception : le changer modifie le jeu et invalide
    /// les fixtures. Mettre à jour cette liste est l'acte explicite qui l'acte.
    /// </summary>
    [Fact]
    public void SimulationPipeline_HasTheDeclaredOrder()
    {
        var c = new GameController();
        Assert.Equal(["Combat", "Achievements"], c.Pipeline.Names);
    }

    [Fact]
    public void LocalizationFiles_HaveTheSameKeys()
    {
        var fr = Localizer.Load(Language.French).Keys.Order().ToList();
        var en = Localizer.Load(Language.English).Keys.Order().ToList();
        Assert.Equal(fr, en);
    }

    /// <summary>
    /// Deux rendus coexistent : Skia + Avalonia (Core.Render, Core.UI) et Godot (Core.Render.Godot,
    /// Core.UI.Godot, App.Godot). Chacun ne s'appuie que sur Model et Controller, jamais sur l'autre :
    /// l'un doit pouvoir être retiré sans toucher au second.
    /// </summary>
    [Fact]
    public void GodotAndSkiaStacks_DoNotReferenceEachOther()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root != null && !File.Exists(Path.Combine(root.FullName, "HeroesOfIdlestan.slnx")))
            root = root.Parent;
        Assert.NotNull(root);

        string[] godot = ["HeroesOfIdlestan.Core.Render.Godot", "HeroesOfIdlestan.Core.UI.Godot", "HeroesOfIdlestan.App.Godot"];
        string[] skia = ["HeroesOfIdlestan.Core.Render", "HeroesOfIdlestan.Core.UI"];

        var offenders = new List<string>();
        foreach (var project in Directory.GetFiles(root.FullName, "*.csproj", SearchOption.AllDirectories))
        {
            var name = Path.GetFileNameWithoutExtension(project);
            var forbidden = godot.Contains(name) ? skia : godot;
            var references = XDocument.Load(project).Descendants("ProjectReference")
                .Select(r => Path.GetFileNameWithoutExtension((string?)r.Attribute("Include") ?? ""));
            offenders.AddRange(references.Where(forbidden.Contains).Select(r => $"{name} → {r}"));
        }

        Assert.True(offenders.Count == 0, "Références entre les rendus Skia et Godot : " + string.Join(", ", offenders));
    }
}
