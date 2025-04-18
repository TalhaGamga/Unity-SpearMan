using System;
using UnityEngine;

[CreateAssetMenu(menuName = "ScriptableObjects/Weapon/Firearm/HitscanProjectileSystem")]
public class HitscanProjectileSystemSO : ProjectileSystemBase
{
    [SerializeField] private HitscanProjectile _hitscanProjectile;

    public override event Action<ProjectileHitInfo> OnProjectileGatheredInfo;

    public override void Init()
    {
        _hitscanProjectile.OnProjectileFiredAndHit += (ProjectileGatheredInfo) => OnProjectileGatheredInfo?.Invoke(ProjectileGatheredInfo);
    }

    public override IProjectile CreateProjectile()
    {
        return _hitscanProjectile;
    }
}