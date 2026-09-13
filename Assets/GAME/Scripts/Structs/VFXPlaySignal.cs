using UnityEngine;


public readonly struct VFXPlaySignal
{
    public readonly SystemType SystemType;
    public readonly VFXType VFXType;
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

    /// <summary>
    /// Already-resolved prefab. Set by callers that own their own effect
    /// catalogue - a weapon's visual pack, say - so they do not have to push
    /// their prefabs into the scene-wide <see cref="VFXSet"/> and collide with
    /// every other actor using the same <see cref="SystemType"/>.
    /// Left null, the manager falls back to the VFXType lookup.
    /// </summary>
    public readonly GameObject Prefab;

    public VFXPlaySignal(
        SystemType systemType,
        VFXType vfxType,
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
        float startDelay = 0f,
        GameObject prefab = null)
    {
        Prefab = prefab;
        SystemType = systemType;
        VFXType = vfxType;
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

    public static VFXPlaySignal Default => new VFXPlaySignal(SystemType.Default, VFXType.None, Vector3.zero, Quaternion.identity, Vector3.zero, Vector3.zero, Vector3.one);
}
