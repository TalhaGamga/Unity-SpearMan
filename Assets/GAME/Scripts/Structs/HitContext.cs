using UnityEngine;

public readonly struct HitContext
{
    private const float MinMotionSqrMagnitude = 0.0001f;

    public Vector3 Velocity { get; }
    public Vector3 Direction { get; }
    public Vector3 Point { get; }
    public Vector3 SlicePlaneNormal { get; }
    public float Speed => Velocity.magnitude;

    public HitContext(
        Vector3 velocity,
        Vector3 point,
        Vector3 fallbackDirection,
        Vector3 bladeDirection)
    {
        Velocity = velocity;
        Point = point;

        if (velocity.sqrMagnitude > MinMotionSqrMagnitude)
            Direction = velocity.normalized;
        else if (fallbackDirection.sqrMagnitude > MinMotionSqrMagnitude)
            Direction = fallbackDirection.normalized;
        else
            Direction = Vector3.zero;

        Vector3 planeNormal = Vector3.Cross(velocity, bladeDirection);
        SlicePlaneNormal = planeNormal.sqrMagnitude > MinMotionSqrMagnitude
            ? planeNormal.normalized
            : Vector3.zero;
    }
}
