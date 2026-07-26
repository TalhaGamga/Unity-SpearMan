using UnityEngine;

[System.Serializable]
public struct PhysicsResponseSettings
{
    public PhysicsAxes TranslationAxes;
    public PhysicsAxes RotationAxes;

    [Min(0f)]
    public float LinearForce;

    [Min(0f)]
    public float AngularMultiplier;

    public VerticalImpactMode VerticalMode;

    [Range(0f, 1f)]
    public float MinimumVerticalRatio;
}
