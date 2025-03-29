using System;
using UnityEngine;

[CreateAssetMenu(menuName = "ScriptableObjects/Weapon/Firearm/HitscanProjectile")]
public class HitscanProjectile : ScriptableObject, IProjectile
{
    public event Action<ProjectileGatheredInfo> OnProjectileFiredAndHit;

    [SerializeField] private float range = 100f;

    public void Fire(Vector3 origin, Vector3 direction, float speed)
    {
        RaycastHit2D hit = Physics2D.Raycast(origin, direction, range);

        if (hit.collider != null)
        {
            Debug.Log($"Hitscan Hit: {hit.collider.gameObject.name}");
        }

        Vector3 endPoint = hit.collider != null ? hit.point : origin + direction * range;

        ProjectileGatheredInfo info = new ProjectileGatheredInfo() { FirePoint = origin, EndPoint = endPoint };

        OnProjectileFiredAndHit?.Invoke(info);
    }
}

public struct ProjectileGatheredInfo
{
    public Vector3 FirePoint;
    public Vector3 EndPoint;
}