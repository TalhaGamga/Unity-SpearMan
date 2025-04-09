using UnityEngine;

public class DeadZoneTargeting : ICameraTargeting
{
    private readonly Transform _anchor;
    public DeadZoneTargeting(Transform player, Vector2 deadZoneSize)
    {
        var anchorGO = new GameObject("DeadZoneAnchor");
        var anchor = anchorGO.AddComponent<CamDeadZoneAnchor>();

        anchor.Target = player;
        anchor.DeadZoneSize = deadZoneSize;
        _anchor = anchor.transform;
    }
    public Transform GetFollowTarget() => _anchor;

    public Transform GetLookAtTarget() => null;
}