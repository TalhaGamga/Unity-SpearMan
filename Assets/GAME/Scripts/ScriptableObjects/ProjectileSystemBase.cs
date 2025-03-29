using System;
using UnityEngine;

public abstract class ProjectileSystemBase : ScriptableObject, IProjectileSystem
{
    public abstract event Action<ProjectileGatheredInfo> OnProjectileGatheredInfo;
    public abstract IProjectile CreateProjectile();
    public abstract void Init();
}