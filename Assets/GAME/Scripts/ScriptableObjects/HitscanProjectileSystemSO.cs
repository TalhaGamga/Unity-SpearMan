using System;
using UnityEngine;


[CreateAssetMenu(menuName = "ScriptableObjects/Weapon/Firearm/HitscanProjectileSystem")]
public class HitscanProjectileSystemSO : ProjectileSystemBase
{
    [SerializeField] private HitscanProjectile _hitscanProjectile;

    public override IProjectile CreateProjectile()
    {
        return _hitscanProjectile;
    }
}