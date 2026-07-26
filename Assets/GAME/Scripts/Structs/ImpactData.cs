using UnityEngine;

[System.Serializable]
public readonly struct ImpactData
{
    public Vector3 Direction { get; }
    public Vector3 AngularDirection { get; }
    public float Force { get; }
    public float AngularImpulse { get; }
    public Vector3 Point { get; }
    public PhysicsAxes TranslationAxes { get; }
    public PhysicsAxes RotationAxes { get; }

    public ImpactData(
        Vector3 direction,
        float force,
        Vector3 point,
        PhysicsAxes translationAxes = PhysicsAxes.XYZ,
        PhysicsAxes rotationAxes = PhysicsAxes.XYZ,
        float angularImpulse = 0f,
        Vector3 angularDirection = default)
    {
        TranslationAxes = PhysicsAxesUtility.Sanitize(translationAxes);
        RotationAxes = PhysicsAxesUtility.Sanitize(rotationAxes);
        PhysicsAxes motionAxes = TranslationAxes | RotationAxes;
        Direction = PhysicsAxesUtility.Direction(direction, motionAxes);
        AngularDirection = angularDirection.sqrMagnitude > Mathf.Epsilon
            ? PhysicsAxesUtility.Direction(angularDirection, motionAxes)
            : Direction;
        Force = Mathf.Max(0f, force);
        AngularImpulse = Mathf.Max(0f, angularImpulse);
        Point = point;
    }

    public ImpactData ForTarget(Vector3 direction, float share)
    {
        share = Mathf.Clamp01(share);
        return new ImpactData(
            direction,
            Force * share,
            Point,
            TranslationAxes,
            RotationAxes,
            AngularImpulse * share,
            AngularDirection
        );
    }
}