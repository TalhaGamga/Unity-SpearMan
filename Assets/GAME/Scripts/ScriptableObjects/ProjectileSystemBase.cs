using UnityEngine;

public abstract class ProjectileSystemBase : ScriptableObject, IProjectileSystem
{
    public abstract IProjectile CreateProjectile();
}