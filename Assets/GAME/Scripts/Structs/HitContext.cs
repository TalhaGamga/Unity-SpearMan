using UnityEngine;

public readonly struct HitContext
{
    private const float MinMotionSqrMagnitude = 0.0001f;
    private readonly Vector3 _fallbackDirection;

    public Vector3 Velocity { get; }
    public Vector3 Direction => GetDirection(PhysicsAxes.XYZ);
    public Vector3 Point { get; }
    public Vector3 SlicePlaneNormal { get; }
    public float Speed => GetSpeed(PhysicsAxes.XYZ);

    public HitContext(
        Vector3 velocity,
        Vector3 point,
        Vector3 fallbackDirection,
        Vector3 bladeDirection)
    {
        Velocity = velocity;
        _fallbackDirection = fallbackDirection;
        Point = point;

        // Slicing is visual geometry, so its plane can still use full 3D blade motion.
        Vector3 planeNormal = Vector3.Cross(velocity, bladeDirection);
        SlicePlaneNormal = planeNormal.sqrMagnitude > MinMotionSqrMagnitude
            ? planeNormal.normalized
            : Vector3.zero;
    }

    public Vector3 GetDirection(PhysicsAxes axes)
    {
        Vector3 velocity = PhysicsAxesUtility.Project(Velocity, axes);
        if (velocity.sqrMagnitude > MinMotionSqrMagnitude)
            return velocity.normalized;

        Vector3 fallback = PhysicsAxesUtility.Project(_fallbackDirection, axes);
        return fallback.sqrMagnitude > MinMotionSqrMagnitude
            ? fallback.normalized
            : Vector3.zero;
    }

    public float GetSpeed(PhysicsAxes axes)
    {
        return PhysicsAxesUtility.Project(Velocity, axes).magnitude;
    }
}
