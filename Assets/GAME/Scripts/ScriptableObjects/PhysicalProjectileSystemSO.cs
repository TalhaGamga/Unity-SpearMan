using System;
using UnityEngine;


[CreateAssetMenu(menuName = "ScriptableObjects/Weapon/Firearm/PhysicalProjectileSystem")]
public class PhysicalProjectileSystemSO : ProjectileSystemBase
{
    [SerializeField] private GameObject _projectilePrefab;

    public override IProjectile CreateProjectile()
    {
        if (_projectilePrefab == null)
        {
            Debug.LogError($"HitscanPrefab is missing {_projectilePrefab.name}");
        }

        GameObject instance = Instantiate(_projectilePrefab);

        IPhysicalProjectile projectile = instance.GetComponent<IPhysicalProjectile>();

        if (projectile != null)
        {
            return projectile;
        }

        return null;
    }
}