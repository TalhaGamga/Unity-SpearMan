using UnityEngine;

public class CamLockOnTargeting : ICameraTargeting
{
    private readonly Transform _midpoint;
    public CamLockOnTargeting(Transform player, Transform boss)
    {
        var anchorGO = new GameObject("CameraMidPointAnchor");
        var midpoint = anchorGO.AddComponent<CamMidPointAnchor>();
        midpoint.TargetA = player;
        midpoint.TargetB = boss;
        _midpoint = midpoint.transform;
    }
    public Transform GetFollowTarget() => _midpoint;

    public Transform GetLookAtTarget() => null;
}