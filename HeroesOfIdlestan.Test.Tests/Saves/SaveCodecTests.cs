using HeroesOfIdlestan.Core.Controller;
using HeroesOfIdlestan.Core.Model.Entities;
using HeroesOfIdlestan.Core.Model.Persistence;

namespace HeroesOfIdlestan.Test.Tests.Saves;

/// <summary>
/// Niveau 2 — chaque format de fichier jamais écrit reste lisible, et une sauvegarde abîmée produit
/// une erreur explicite (<see cref="SaveCorruptedException"/>) plutôt qu'un état à moitié chargé.
/// Ajouter un cas ici à chaque nouveau format ; n'en retirer aucun.
/// </summary>
public class SaveCodecTests
{
    /// <summary>Format 0 : JSON brut, tel qu'on l'écrit à la main pour un test ou un débogage.</summary>
    private const string RawJsonSave =
        """{"SavedGameVersion":"0.1.0","Gold":42,"Heroes":[{"Class":"Warrior","Level":3,"Health":50}]}""";

    [Fact]
    public void Format0_RawJson_IsReadable()
    {
        var c = new GameController();
        c.Load(RawJsonSave);

        Assert.Equal<long>(42, c.State.Gold);
        var warrior = Assert.Single(c.State.Heroes);
        Assert.Equal(HeroClass.Warrior, warrior.Class);
        Assert.Equal(3, warrior.Level);
        Assert.Equal<long>(50, warrior.Health);
    }

    [Fact]
    public void Format1_XorBase64_IsReadable()
    {
        var c = new GameController();
        c.Load(SaveCodec.Encode(RawJsonSave));
        Assert.Equal<long>(42, c.State.Gold);
    }

    [Fact]
    public void Encode_Then_Decode_IsIdentity()
    {
        const string json = """{"a":"é€😀"}""";
        Assert.Equal(json, SaveCodec.Decode(SaveCodec.Encode(json)));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not a save at all")]
    [InlineData("SGVsbG8gd29ybGQ=")] // Base64 valide qui ne décode pas en JSON
    [InlineData("{ \"Gold\": ")]     // JSON tronqué
    [InlineData("[1,2,3]")]          // JSON valide mais pas un objet
    public void CorruptedContent_ThrowsSaveCorrupted(string content)
    {
        var c = new GameController();
        Assert.Throws<SaveCorruptedException>(() => c.Load(content));
    }

    /// <summary>Une valeur d'enum renommée sans migration rend la sauvegarde illisible : jamais à moitié chargée.</summary>
    [Fact]
    public void UnknownEnumValue_ThrowsSaveCorrupted()
    {
        var c = new GameController();
        Assert.Throws<SaveCorruptedException>(() =>
            c.Load("""{"Heroes":[{"Class":"Dragon"}]}"""));
    }

    [Fact]
    public void FailedLoad_LeavesPreviousStateUntouched()
    {
        var c = new GameController();
        c.NewGame(seed: 3);
        var before = c.Export();

        Assert.Throws<SaveCorruptedException>(() => c.Load("garbage"));

        Assert.Equal(SaveCodec.Decode(before), SaveCodec.Decode(c.Export()));
    }
}
