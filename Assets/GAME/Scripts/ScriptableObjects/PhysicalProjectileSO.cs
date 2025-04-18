using System;
using UnityEngine;

public class PhysicalProjectile : MonoBehaviour, IProjectile
{
    public RaycastHit2D HitData { get; set; }

    public event Action OnHit;
    public event Action<ProjectileHitInfo> OnProjectileFiredAndHit;

    public void Fire(Vector3 origin, Vector3 direction, float speed)
    {
        Debug.Log("Physical Projectile has been thrown");
    }
}