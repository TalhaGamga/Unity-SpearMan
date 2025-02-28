using System;
using UnityEngine;

[CreateAssetMenu(menuName = "ScriptableObjects/Weapon/Firearm/FireSystem")]
public class FireSystemSO : ScriptableObject, IFireSystem
{
    public IProjectileSystem ProjectileSystem => projectileSystemSO;

    public Action OnFired { get; set; }


    [SerializeField] private ProjectileSystemSO projectileSystemSO;
}