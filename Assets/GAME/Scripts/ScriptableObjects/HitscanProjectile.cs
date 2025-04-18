using System;
using UnityEngine;

[CreateAssetMenu(menuName = "ScriptableObjects/Weapon/Firearm/HitscanProjectile")]
public class HitscanProjectile : ScriptableObject, IProjectile
{
    public event Action<ProjectileHitInfo> OnProjectileFiredAndHit;
    [SerializeField] private float range = 100f;

    public void Fire(Vector3 origin, Vector3 direction, float speed)
    {
        RaycastHit2D hit = Physics2D.Raycast(origin, direction, range);

        if (hit.collider != null)
        {
            //Debug.Log($"Hitscan Hit: {hit.collider.gameObject.name}");
        }

        Vector3 endPoint = hit.collider != null ? hit.point : origin + direction * range;

        ProjectileHitInfo info = new ProjectileHitInfo() { FirePoint = origin, EndPoint = endPoint, HitObject = hit.collider.gameObject };

        OnProjectileFiredAndHit?.Invoke(info);
    }
}
