namespace HeroesOfIdlestan.Tests.TestUtilities;

/// <summary>
/// Test sauté sauf si <c>BLANK_MANUAL_TESTS=1</c>. Réservé aux actions volontaires, comme la
/// régénération des fixtures : « lancer tous les tests » ne doit jamais réécrire de sauvegarde.
/// </summary>
public sealed class ManualFactAttribute : FactAttribute
{
    public const string EnvironmentVariable = "BLANK_MANUAL_TESTS";

    public ManualFactAttribute()
    {
        if (Environment.GetEnvironmentVariable(EnvironmentVariable) != "1")
            Skip = $"Test manuel — définir {EnvironmentVariable}=1 et le lancer explicitement (--filter).";
    }
}
