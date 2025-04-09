using UnityEngine;

public class CamDeadZoneAnchor : MonoBehaviour
{
    public Transform Target;
    public Vector2 DeadZoneSize = new Vector2(2f, 1f);

    private Vector3 _anchorPosition;

    private void Start()
    {
        _anchorPosition = Target.position;
    }

    private void LateUpdate()
    {
        Vector3 offset = Target.position - _anchorPosition;

        if (Mathf.Abs(offset.x) > DeadZoneSize.x)
            _anchorPosition.x = Target.position.x - Mathf.Sign(offset.x) * DeadZoneSize.x;

        if (Mathf.Abs(offset.y) > DeadZoneSize.y)
            _anchorPosition.y = Target.position.y - Mathf.Sign(offset.y) * DeadZoneSize.y;

        transform.position = _anchorPosition;
    }
}