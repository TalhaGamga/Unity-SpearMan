using System;
using UnityEngine;


[CreateAssetMenu(menuName ="ScriptableObjects/Weapon/Firearm/ProjectileSystem")]
public class ProjectileSystemSO : ScriptableObject,IProjectileSystem
{
    public Action OnProjectileCreated { get; set; }

    public IProjectile CreateProjectile()
    {
        throw new NotImplementedException();
    }
}