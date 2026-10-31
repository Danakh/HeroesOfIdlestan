using Godot;

namespace HeroesOfIdlestan.Core.Render.Godot.Battle;

/// <summary>
/// Caméra en orbite autour du champ de bataille. Souris : glisser (gauche ou droit) pour tourner,
/// molette pour zoomer, bouton du milieu pour déplacer. Pavé tactile : pincer pour zoomer, deux doigts
/// pour tourner. Écran tactile : glisser pour tourner.
/// </summary>
public sealed class CameraRig
{
    private const float DefaultDistance = 8.5f;
    private const float MinDistance = 4f;
    private const float MaxDistance = 30f;
    private const float DefaultPitch = -0.3f; // ≈ -17°
    private const float MinPitch = -1.4f;
    private const float MaxPitch = -0.05f;
    private const float OrbitSpeed = 0.006f;
    private static readonly Vector3 DefaultTarget = new(0, 0.9f, 0);

    private Vector3 _target;
    private float _yaw;
    private float _pitch;
    private float _distance;
    private bool _orbiting;
    private bool _panning;

    public CameraRig()
    {
        Camera = new Camera3D { Name = "Camera", Fov = 45f, Current = true };
        Reset();
    }

    public Camera3D Camera { get; }

    /// <summary>
    /// Cadre tout le champ de bataille, vu de face et légèrement d'en haut. <paramref name="distanceScale"/>
    /// recule la caméra pour un champ de bataille plus profond que celui par défaut.
    /// </summary>
    public void Reset(float distanceScale = 1f)
    {
        _target = DefaultTarget;
        _yaw = 0;
        _pitch = DefaultPitch;
        _distance = Mathf.Clamp(DefaultDistance * distanceScale, MinDistance, MaxDistance);
        _orbiting = _panning = false;
        Apply();
    }

    /// <summary>Vrai si l'événement a servi à la caméra.</summary>
    public bool HandleInput(InputEvent input)
    {
        switch (input)
        {
            case InputEventMouseButton { ButtonIndex: MouseButton.WheelUp, Pressed: true }:
                Zoom(0.9f);
                return true;
            case InputEventMouseButton { ButtonIndex: MouseButton.WheelDown, Pressed: true }:
                Zoom(1.1f);
                return true;
            case InputEventMouseButton { ButtonIndex: MouseButton.Left or MouseButton.Right } button:
                _orbiting = button.Pressed;
                return true;
            case InputEventMouseButton { ButtonIndex: MouseButton.Middle } button:
                _panning = button.Pressed;
                return true;
            case InputEventMouseMotion motion when _orbiting:
                Orbit(motion.Relative);
                return true;
            case InputEventMouseMotion motion when _panning:
                Pan(motion.Relative);
                return true;
            case InputEventMagnifyGesture magnify when magnify.Factor > 0:
                Zoom(1f / magnify.Factor);
                return true;
            case InputEventPanGesture pan:
                Orbit(pan.Delta * 8f);
                return true;
            case InputEventScreenDrag drag:
                Orbit(drag.Relative);
                return true;
        }
        return false;
    }

    private void Orbit(Vector2 delta)
    {
        _yaw -= delta.X * OrbitSpeed;
        _pitch = Mathf.Clamp(_pitch - delta.Y * OrbitSpeed, MinPitch, MaxPitch);
        Apply();
    }

    private void Pan(Vector2 delta)
    {
        // Déplacement dans le plan du sol, à vitesse proportionnelle à la distance.
        var basis = Basis.FromEuler(new Vector3(0, _yaw, 0));
        float scale = _distance * 0.0015f;
        _target += basis * new Vector3(-delta.X * scale, 0, -delta.Y * scale);
        Apply();
    }

    private void Zoom(float factor)
    {
        _distance = Mathf.Clamp(_distance * factor, MinDistance, MaxDistance);
        Apply();
    }

    private void Apply()
    {
        // Calcul hors de l'arbre : LookAt exige un nœud déjà attaché, LookingAt non.
        var offset = Basis.FromEuler(new Vector3(_pitch, _yaw, 0)) * new Vector3(0, 0, _distance);
        Camera.Transform = new Transform3D(Basis.Identity, _target + offset).LookingAt(_target, Vector3.Up);
    }
}
