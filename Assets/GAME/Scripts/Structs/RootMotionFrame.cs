using UnityEngine;

public readonly struct RootMotionFrame
{
    public readonly Vector3 DeltaPosition;
    public readonly Quaternion DeltaRotation;

    public RootMotionFrame(Vector3 pos, Quaternion rot)
    {
        DeltaPosition = pos;
        DeltaRotation = rot;
    }
}
