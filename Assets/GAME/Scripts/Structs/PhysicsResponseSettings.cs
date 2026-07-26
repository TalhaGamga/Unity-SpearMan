using UnityEngine;

[System.Serializable]
public struct PhysicsResponseSettings
{
    public PhysicsAxes TranslationAxes;
    public PhysicsAxes RotationAxes;

    [Min(0f)]
    public float LinearMultiplier;

    [Min(0f)]
    public float AngularMultiplier;
}
