using System;
using UnityEngine;

[CreateAssetMenu(menuName = "ScriptableObjects/Weapon/Firearm/FireSystem")]
public class FireSystemSO : ScriptableObject, IFireSystem
{
    public IProjectileSystem ProjectileSystem => projectileSystemSO;

    public float FireRate => _fireRate;

    public event Action OnFired;

    public bool CanFire => Time.time >= _lastFireTime + _fireRate;

    [SerializeField] private ProjectileSystemBase projectileSystemSO;
    [SerializeField] private float _fireRate = 0.2f;
    [SerializeField] private float projectileSpeed = 50f;
    [SerializeField] private float damage = 10f;

    private float _lastFireTime = 0f;

    public void Init()
    {
        _lastFireTime = 0;
    }

    public void Fire(Vector3 origin, Vector3 direction)
    {
        Debug.Log("Firee");
        //if (!CanFire) return;

        _lastFireTime = Time.time;

        IProjectile projectile = projectileSystemSO.CreateProjectile();
        projectile.Fire(origin, direction, projectileSpeed, damage);
        Debug.Log("FireSystemSO Fired");
        OnFired?.Invoke();
    }
}