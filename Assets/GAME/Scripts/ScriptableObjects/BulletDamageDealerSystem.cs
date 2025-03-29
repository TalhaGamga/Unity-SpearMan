using UnityEngine;

[CreateAssetMenu(menuName = "ScriptableObjects/Weapon/Firearm/DamageDealer")]
public class BulletDamageDealerSystem : ScriptableObject, IBulletDamageDealerSystem
{
    public void DealDamage(IDamagable Target, float Damage, Vector3 HitPoint, float CritRate, float impulse)
    {
        Debug.Log("Damage Dealt");
    }
}