using System;
using UnityEngine;

[CreateAssetMenu(menuName = "ScriptableObjects/Weapon/Firearm/AmmoSystem")]
public class AmmoSystemSO : ScriptableObject,IAmmoSystem
{
    public int MagazinwAmmo { get; set; }
    public int MagazineCount { get; set; }
    public Action OnMagazineConsumed { get; set; }

    public void ResetMagazine()
    {
    }
}