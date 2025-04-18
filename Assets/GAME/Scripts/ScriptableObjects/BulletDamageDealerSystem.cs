using UnityEngine;

[CreateAssetMenu(menuName = "ScriptableObjects/Weapon/Firearm/DamageDealer")]
public class BulletDamageDealerSystem : ScriptableObject, IBulletDamageDealerSystem
{
    public void DealDamage(IDamageable Target, float Damage, Vector3 HitPoint, float CritRate, float impulse)
    {
        Debug.Log("Damage Dealt");
    }
}