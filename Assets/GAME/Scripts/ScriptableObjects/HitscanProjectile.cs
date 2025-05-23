using System;
using UnityEngine;

[CreateAssetMenu(menuName = "ScriptableObjects/Weapon/Firearm/HitscanProjectile")]
public class HitscanProjectile : ScriptableObject, IProjectile
{
    public event Action<ProjectileHitInfo> OnProjectileFiredAndHit;

    [SerializeField] private float range = 100f;
    [SerializeField] private LayerMask hitMask3D = ~0;
    [SerializeField] private LayerMask hitMask2D = ~0;

    public void Fire(Vector3 origin, Vector3 direction, float speed)
    {
        GameObject hitObject = null;
        Vector3 endPoint = origin + direction * range;
        float closestDistance = float.MaxValue;
        Vector3 _direction = Vector3.zero;

        // 3D Raycast
        if (Physics.Raycast(origin, direction, out RaycastHit hit3D, range, hitMask3D))
        {
            hitObject = hit3D.collider.gameObject;
            endPoint = hit3D.point;
            closestDistance = hit3D.distance;
            _direction = direction;
        }

        // 2D Raycast
        RaycastHit2D hit2D = Physics2D.Raycast(origin, direction, range, hitMask2D);
        if (hit2D.collider != null && hit2D.distance < closestDistance)
        {
            hitObject = hit2D.collider.gameObject;
            endPoint = hit2D.point;
            closestDistance = hit2D.distance;
            _direction = direction;
        }

        // Construct and dispatch info
        ProjectileHitInfo info = new ProjectileHitInfo
        {
            FirePoint = origin,
            EndPoint = endPoint,
            HitObject = hitObject,
            Direction = _direction
        };

        OnProjectileFiredAndHit?.Invoke(info);
    }
}
public enum HitscanMode
{
    Use3D,
    Use2D
}