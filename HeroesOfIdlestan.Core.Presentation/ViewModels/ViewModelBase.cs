using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace HeroesOfIdlestan.Core.Presentation.ViewModels;

/// <summary>
/// Base des ViewModels de l'overlay. <see cref="SetProperty{T}"/> ne notifie que si la valeur change
/// réellement : l'état est repoussé par sondage, et notifier à chaque passage ferait relayouter
/// la vue (Avalonia ou Godot) en continu pour des valeurs identiques.
/// </summary>
public abstract class ViewModelBase : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        return true;
    }
}
