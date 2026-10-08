using UnityEngine;

[System.Serializable]
public struct PierceSettings
{
    [Min(0f)] public float MaxFollowDuration;
    [Min(0f)] public float MaxFollowSpeed;
    [Min(0f)] public float ReleaseForce;
    [Range(0f, 1f)] public float ReleaseVerticalScale;
    [Range(0f, 1f)] public float MinimumReleaseVerticalRatio;
    [Range(0f, 1f)] public float MaximumReleaseVerticalRatio;
}
