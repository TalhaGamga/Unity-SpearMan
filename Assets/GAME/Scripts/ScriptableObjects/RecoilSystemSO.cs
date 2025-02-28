using System;
using UnityEngine;

[CreateAssetMenu(menuName = "ScriptableObjects/Weapon/Firearm/RecoilSystem")]
public class RecoilSystemSO : ScriptableObject, IRecoilSystem
{
    public int KickbackAmount { get; set; }
    public Action<float> OnKickback { get; set; }
}