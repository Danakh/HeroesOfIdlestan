using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using HeroesOfIdlestan.Core.Model.Animation;
using HeroesOfIdlestan.Core.Render.Animation;

namespace HeroesOfIdlestan.Tool.Animation;

/// <summary>
/// Panneau de gauche (squelette, habillage, animations, lecture) et aperçu à droite. Outil de
/// développement : les contrôles pilotent l'aperçu directement, sans ViewModel.
/// </summary>
public sealed class MainView : DockPanel, IDisposable
{
    private static readonly TimeSpan SyncInterval = TimeSpan.FromMilliseconds(50);

    private readonly string _root;
    private readonly AnimationPreview _preview = new();
    private readonly DispatcherTimer _sync;

    private readonly ComboBox _skeletons = new() { HorizontalAlignment = HorizontalAlignment.Stretch };
    private readonly ComboBox _skins = new() { HorizontalAlignment = HorizontalAlignment.Stretch };
    private readonly ListBox _animations = new() { MinHeight = 80 };
    private readonly Button _playPause = new() { Content = "Pause" };
    private readonly Slider _timeline = new() { Minimum = 0, Maximum = 1 };
    private readonly TextBlock _timeLabel = new() { Foreground = Brushes.Gray };
    private readonly Slider _speed = new() { Minimum = 0.1, Maximum = 2, Value = 1 };
    private readonly TextBlock _speedLabel = new();
    private readonly CheckBox _loop = new() { Content = "Boucle" };
    private readonly CheckBox _mix = new() { Content = "Fondu entre animations", IsChecked = true };
    private readonly CheckBox _showSkin = new() { Content = "Habillage", IsChecked = true };
    private readonly CheckBox _showBones = new() { Content = "Os" };
    private readonly CheckBox _mirror = new() { Content = "Miroir" };
    private readonly TextBlock _errors = new() { Foreground = Brushes.IndianRed, TextWrapping = TextWrapping.Wrap };

    private AnimationLibrary _library;
    private SkeletonEntry? _skeleton;
    private string? _skinError;

    /// <summary>Vrai pendant qu'on écrit les contrôles depuis le code : leurs événements ne sont pas des actions.</summary>
    private bool _updating;

    public MainView(string root)
    {
        _root = root;
        _library = AnimationLibrary.Load(root);

        var refresh = new Button { Content = "Recharger les fichiers", HorizontalAlignment = HorizontalAlignment.Stretch };
        var restart = new Button { Content = "Rejouer" };
        var rootLabel = new TextBlock { Text = root, Foreground = Brushes.Gray, FontSize = 11, TextWrapping = TextWrapping.Wrap };

        var panel = new StackPanel
        {
            Spacing = 6,
            Children =
            {
                Header("Squelette"), _skeletons,
                Header("Habillage"), _skins,
                Header("Animations"), _animations,
                new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Children = { _playPause, restart } },
                _timeline, _timeLabel,
                _speedLabel, _speed,
                _loop, _mix,
                Header("Affichage"), _showSkin, _showBones, _mirror,
                new Separator(), refresh, rootLabel, _errors,
            },
        };
        var side = new Border
        {
            Width = 280,
            Padding = new Thickness(12),
            Background = new SolidColorBrush(Color.FromRgb(0x10, 0x14, 0x1A)),
            Child = new ScrollViewer { Content = panel },
        };
        SetDock(side, Dock.Left);
        Children.Add(side);
        Children.Add(new AnimationCanvas(_preview));

        _skeletons.SelectionChanged += (_, _) => { if (!_updating) SelectSkeleton(_skeletons.SelectedIndex); };
        _skins.SelectionChanged += (_, _) => { if (!_updating) SelectSkin(_skins.SelectedIndex); };
        _animations.SelectionChanged += (_, _) => { if (!_updating) SelectAnimation(_animations.SelectedIndex); };
        _playPause.Click += (_, _) => _preview.TogglePause();
        restart.Click += (_, _) => _preview.Restart();
        refresh.Click += (_, _) => Reload();
        _timeline.PropertyChanged += (_, e) =>
        {
            if (e.Property == RangeBase.ValueProperty && !_updating) _preview.Seek((float)_timeline.Value);
        };
        _speed.PropertyChanged += (_, e) =>
        {
            if (e.Property != RangeBase.ValueProperty) return;
            _preview.SetSpeed((float)_speed.Value);
            _speedLabel.Text = $"Vitesse ×{_speed.Value.ToString("0.00", CultureInfo.InvariantCulture)}";
        };
        _speedLabel.Text = "Vitesse ×1.00";
        _loop.IsCheckedChanged += (_, _) => { if (!_updating) _preview.SetLoop(_loop.IsChecked == true); };
        _mix.IsCheckedChanged += (_, _) => _preview.SetMix(_mix.IsChecked == true);
        _showSkin.IsCheckedChanged += (_, _) => _preview.ShowSkin = _showSkin.IsChecked == true;
        _showBones.IsCheckedChanged += (_, _) => _preview.ShowBones = _showBones.IsChecked == true;
        _mirror.IsCheckedChanged += (_, _) => _preview.Mirror = _mirror.IsChecked == true;

        FillSkeletons(selectId: null);

        _sync = new DispatcherTimer(SyncInterval, DispatcherPriority.Normal, (_, _) => SyncFromPreview());
        _sync.Start();
    }

    private static TextBlock Header(string text) =>
        new() { Text = text, FontWeight = FontWeight.SemiBold, Margin = new Thickness(0, 6, 0, 0) };

    // ── Sélection ─────────────────────────────────────────────────────────

    private void FillSkeletons(string? selectId)
    {
        Write(() => _skeletons.ItemsSource = _library.Skeletons.Select(s => s.Skeleton.Id).ToList());
        int index = Math.Max(0, _library.Skeletons.FindIndex(s => s.Skeleton.Id == selectId));
        Write(() => _skeletons.SelectedIndex = _library.Skeletons.Count > 0 ? index : -1);
        SelectSkeleton(_skeletons.SelectedIndex);
    }

    /// <summary>Change de squelette : ses habillages et ses animations remplacent les précédents.</summary>
    private void SelectSkeleton(int index, string? skinId = null, string? animationId = null)
    {
        _skeleton = index >= 0 && index < _library.Skeletons.Count ? _library.Skeletons[index] : null;
        _preview.SetSkeleton(_skeleton?.Skeleton);

        var skins = _skeleton?.Skins ?? [];
        var animations = _skeleton?.Animations ?? [];
        int skin = Math.Max(0, skins.FindIndex(s => s.Skin.Id == skinId));
        int animation = Math.Max(0, animations.FindIndex(a => a.Id == animationId));
        Write(() =>
        {
            _skins.ItemsSource = skins.Select(s => s.Skin.Id).ToList();
            _skins.SelectedIndex = skins.Count > 0 ? skin : -1;
            _animations.ItemsSource = animations.Select(a => a.Loop ? $"{a.Id}  (boucle)" : a.Id).ToList();
            _animations.SelectedIndex = animations.Count > 0 ? animation : -1;
        });
        SelectSkin(_skins.SelectedIndex);
        SelectAnimation(_animations.SelectedIndex);
    }

    private void SelectSkin(int index)
    {
        _skinError = null;
        if (_skeleton is null || index < 0 || index >= _skeleton.Skins.Count)
        {
            _preview.SetSkin(null);
        }
        else
        {
            var entry = _skeleton.Skins[index];
            try
            {
                _preview.SetSkin(new SkinRenderer(_skeleton.Skeleton, entry.Skin, path =>
                {
                    var file = Path.Combine(entry.Directory, path);
                    return File.Exists(file) ? File.OpenRead(file) : null;
                }));
            }
            catch (Exception ex)
            {
                // Un SVG illisible ne doit pas fermer l'outil : on montre les os et l'erreur.
                _preview.SetSkin(null);
                _skinError = $"Habillage '{entry.Skin.Id}' : {ex.Message}";
            }
        }
        ShowErrors();
    }

    private void SelectAnimation(int index)
    {
        if (_skeleton is null || index < 0 || index >= _skeleton.Animations.Count) return;
        var animation = _skeleton.Animations[index];
        _preview.Play(animation);
        Write(() =>
        {
            _loop.IsChecked = animation.Loop;
            _timeline.Maximum = animation.Duration;
        });
    }

    /// <summary>Relit le dossier en gardant la sélection courante quand elle existe encore.</summary>
    private void Reload()
    {
        var skeletonId = _skeleton?.Skeleton.Id;
        var skinId = _skeleton?.Skins.ElementAtOrDefault(_skins.SelectedIndex)?.Skin.Id;
        var animationId = _skeleton?.Animations.ElementAtOrDefault(_animations.SelectedIndex)?.Id;

        _library = AnimationLibrary.Load(_root);
        Write(() => _skeletons.ItemsSource = _library.Skeletons.Select(s => s.Skeleton.Id).ToList());
        int index = _library.Skeletons.FindIndex(s => s.Skeleton.Id == skeletonId);
        if (index < 0 && _library.Skeletons.Count > 0) index = 0;
        Write(() => _skeletons.SelectedIndex = index);
        SelectSkeleton(index, skinId, animationId);
    }

    // ── Synchro aperçu → panneau ──────────────────────────────────────────

    private void SyncFromPreview()
    {
        var s = _preview.Snapshot();
        Write(() => _timeline.Value = s.Time);
        _timeLabel.Text = string.Create(CultureInfo.InvariantCulture, $"{s.Time:0.00} / {s.Duration:0.00} s");
        _playPause.Content = s.Paused || s.Finished ? "Lecture" : "Pause";
    }

    private void ShowErrors()
    {
        var lines = _library.Errors.Concat(_skinError is null ? [] : [_skinError]).ToList();
        _errors.Text = string.Join(Environment.NewLine + Environment.NewLine, lines);
        _errors.IsVisible = lines.Count > 0;
    }

    private void Write(Action action)
    {
        _updating = true;
        try { action(); }
        finally { _updating = false; }
    }

    public void Dispose()
    {
        _sync.Stop();
        _preview.Dispose();
    }
}
