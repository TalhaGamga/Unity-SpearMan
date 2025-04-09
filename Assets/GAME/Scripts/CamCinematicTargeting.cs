using UnityEngine;

public class CamCinematicTargeting : ICameraTargeting
{
    private readonly Transform _cinematicAnchor;

    public CamCinematicTargeting(Transform anchor)
    {
        _cinematicAnchor = anchor;
    }

    public Transform GetFollowTarget() => _cinematicAnchor;

    public Transform GetLookAtTarget() => null;
}