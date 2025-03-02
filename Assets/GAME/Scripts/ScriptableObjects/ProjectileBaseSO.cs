using UnityEngine;

public abstract class ProjectileBaseSO : ScriptableObject, IProjectile
{
    public abstract void Fire(Vector3 origin, Vector3 direction, float speed, float damage);
}
