using System.ComponentModel;
using Godot;

namespace HeroesOfIdlestan.Core.UI.Godot.Views;

/// <summary>
/// Base des vues liées à une scène d'UI éditée dans Godot (dans le projet App.Godot) : instancie la
/// scène, retrouve ses nœuds par nom unique et recopie chaque propriété notifiée du ViewModel dans
/// son contrôle — Godot n'a pas de binding.
///
/// <para><b>Contrat avec la scène</b> : les nœuds liés portent un nom unique (<c>%Gold</c>…, case
/// « Access as Unique Name » de l'éditeur) ; leur place dans l'arbre est libre. Un nœud manquant ou
/// du mauvais type lève à la construction, au démarrage du jeu, plutôt que de laisser une vue muette.
/// La scène ne porte aucun script : un script Godot doit vivre dans l'assembly du head.</para>
/// </summary>
public abstract class SceneView
{
    private readonly string _scenePath;
    private readonly INotifyPropertyChanged _viewModel;
    private readonly Dictionary<string, Action> _bindings = [];

    protected SceneView(string scenePath, INotifyPropertyChanged viewModel)
    {
        _scenePath = scenePath;
        _viewModel = viewModel;

        var scene = GD.Load<PackedScene>(scenePath)
            ?? throw new InvalidOperationException($"Scène introuvable : {scenePath}");
        Root = scene.Instantiate<Control>();
        viewModel.PropertyChanged += OnPropertyChanged;
    }

    public Control Root { get; }

    protected T Node<T>(string uniqueName) where T : Node =>
        Root.GetNodeOrNull<T>($"%{uniqueName}")
        ?? throw new InvalidOperationException(
            $"{_scenePath} : nœud %{uniqueName} ({typeof(T).Name}) absent, ou sans « Access as Unique Name ».");

    private void OnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != null && _bindings.TryGetValue(e.PropertyName, out var apply)) apply();
    }

    /// <summary>Applique tout de suite, puis à chaque notification de <paramref name="property"/>.</summary>
    protected void Bind(string property, Action apply)
    {
        _bindings[property] = _bindings.TryGetValue(property, out var previous) ? previous + apply : apply;
        apply();
    }

    protected void BindText(Label label, string property, Func<string> text) =>
        Bind(property, () => label.Text = text());

    protected void BindButton(Button button, string property, Func<string> text, Action pressed)
    {
        button.Pressed += pressed;
        Bind(property, () => button.Text = text());
    }

    public void Detach() => _viewModel.PropertyChanged -= OnPropertyChanged;
}
