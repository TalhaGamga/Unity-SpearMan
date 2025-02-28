using UnityEngine;

[CreateAssetMenu(menuName = "ScriptableObjects/Weapon/Firearm/HitscanProjectile")]
public class HitscanProjectile : ScriptableObject, IHitscanProjectile
{
    public void Fire(Vector2 origin, Vector2 direction, float speed, float damage)
    {
        Debug.Log("Hitscan Projectile has been thrown");
    }
}