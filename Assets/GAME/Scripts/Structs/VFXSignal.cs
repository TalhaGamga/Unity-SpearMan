using UnityEngine;


public readonly struct VFXSignal
{
    public readonly VFXType Type;
    public readonly Vector3 Position;
    public readonly Quaternion Rotation;
    public readonly Vector3 PositionOffset;
    public readonly Vector3 RotationOffset;
    public readonly Vector3 Scale;
    public readonly Transform Parent;
    public readonly bool FollowParent;
    public readonly bool OneShot;
    public readonly float Lifetime;
    public readonly int InstanceId;
    public readonly float PlaybackRate;
    public readonly float StartDelay;
    public VFXSignal(
        VFXType type,
        Vector3 position,
        Quaternion rotation,
        Vector3 positionOffset,
        Vector3 rotationOffset,
        Vector3 scale,
        Transform parent = null,
        bool followParent = false,
        bool oneShot = true,
        float lifetime = 0f,
        int instanceId = 0,
        float playbackRate = 1f,
        float startDelay = 0f)
    {
        Type = type;
        Position = position;
        Rotation = rotation;
        PositionOffset = positionOffset;
        RotationOffset = rotationOffset;
        Scale = scale;
        Parent = parent;
        FollowParent = followParent;
        OneShot = oneShot;
        Lifetime = lifetime;
        InstanceId = instanceId;
        PlaybackRate = playbackRate;
        StartDelay = startDelay;
    }

    public static VFXSignal Default => new VFXSignal(VFXType.None, Vector3.zero, Quaternion.identity, Vector3.zero, Vector3.zero,Vector3.one);
}
