using UnityEngine;

public class CamFollowTargeting : ICameraTargeting
{
    private readonly Transform _target;
    public CamFollowTargeting(Transform target)
    {
        _target = target;
    }

    public Transform GetFollowTarget() => _target;

    public Transform GetLookAtTarget() => null;
}
