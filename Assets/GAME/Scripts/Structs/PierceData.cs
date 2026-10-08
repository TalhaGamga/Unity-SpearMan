using UnityEngine;

/// <summary>Runtime contact data for a controlled spear-tip follow.</summary>
public readonly struct PierceData
{
    public IPierceMotionSource Source { get; }
    public Vector3 Point { get; }
    public Vector3 Direction { get; }
    public Vector3 AttackerForward { get; }
    public PierceSettings Settings { get; }

    public PierceData(
        IPierceMotionSource source,
        Vector3 point,
        Vector3 direction,
        Vector3 attackerForward,
        PierceSettings settings)
    {
        Source = source;
        Point = point;
        Direction = PhysicsAxesUtility.Direction(direction, PhysicsAxes.YZ);
        AttackerForward = PhysicsAxesUtility.Direction(
            attackerForward,
            PhysicsAxes.YZ
        );
        Settings = settings;
    }
}
