using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace HeroesOfIdlestan.Core.Localization;

[JsonConverter(typeof(JsonStringEnumConverter<Language>))]
public enum Language
{
    French,
    English,
}

/// <summary>
/// Traductions lues depuis <c>Resources/Localization/{fr,en}.json</c> (ressources embarquées).
/// Une clé absente s'affiche telle quelle, entre crochets : visible en jeu, sans planter.
/// Les deux fichiers doivent rester synchronisés — <c>LocalizationTests</c> le vérifie.
/// </summary>
public sealed class Localizer
{
    private Dictionary<string, string> _entries = [];

    public Language Language { get; private set; }

    public Localizer(Language language = Language.French) => SetLanguage(language);

    public void SetLanguage(Language language)
    {
        Language = language;
        _entries = Load(language);
    }

    public string Get(string key) => _entries.TryGetValue(key, out var value) ? value : $"[{key}]";

    public string GetFormatted(string key, params object[] args) =>
        string.Format(CultureInfo.CurrentCulture, Get(key), args);

    public static Dictionary<string, string> Load(Language language)
    {
        var file = language == Language.French ? "fr" : "en";
        var assembly = typeof(Localizer).Assembly;
        var name = $"HeroesOfIdlestan.Core.Resources.Localization.{file}.json";
        using var stream = assembly.GetManifestResourceStream(name)
                           ?? throw new InvalidOperationException($"Ressource introuvable : {name}");
        return JsonSerializer.Deserialize<Dictionary<string, string>>(stream) ?? [];
    }
}
