using HeroesOfIdlestan.Core.Controller.Meta;
using HeroesOfIdlestan.Core.Model.State;

namespace HeroesOfIdlestan.Core.Controller.Platform;

/// <summary>
/// Boutique de plateforme (Steam, Google Play…) : succès, sauvegarde cloud. Toute méthode doit être
/// sans effet, sans lever, quand le store est indisponible.
/// </summary>
public interface IStoreService : IDisposable
{
    string Name { get; }
    bool IsAvailable { get; }

    /// <summary>Débloque des succès ; redemander un succès déjà débloqué est sans effet.</summary>
    void UnlockAchievements(IReadOnlyList<string> achievementIds);

    void SaveCloudFile(string fileName, string content);

    string? LoadCloudFile(string fileName);
}

/// <summary>Agrège les stores disponibles et leur relaie les succès.</summary>
public sealed class StoreController : IDisposable
{
    private readonly List<IStoreService> _active;
    private AchievementSystem? _connected;

    public StoreController(IEnumerable<IStoreService>? services = null)
    {
        _active = (services ?? []).Where(s => s.IsAvailable).ToList();
    }

    public IReadOnlyList<IStoreService> ActiveServices => _active;

    /// <summary>
    /// S'abonne aux déblocages et renvoie tout l'historique de la partie : un succès obtenu pendant que
    /// le store était déconnecté ne serait sinon jamais rattrapé.
    /// </summary>
    public void Connect(AchievementSystem achievements, GameState state)
    {
        if (_connected != null) _connected.Unlocked -= OnUnlocked;
        _connected = achievements;
        achievements.Unlocked += OnUnlocked;
        if (state.CompletedAchievements.Count > 0)
            foreach (var store in _active) store.UnlockAchievements(state.CompletedAchievements.ToList());
    }

    private void OnUnlocked(string id)
    {
        foreach (var store in _active) store.UnlockAchievements([id]);
    }

    public void Dispose()
    {
        if (_connected != null) _connected.Unlocked -= OnUnlocked;
        foreach (var store in _active) store.Dispose();
    }
}
