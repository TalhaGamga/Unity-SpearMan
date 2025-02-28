using UnityEngine;

[CreateAssetMenu(menuName = "ScriptableObjects/Weapon/Firearm/PhysicalProjectile")]
public class PhysicalProjectileSO : ScriptableObject, IProjectile
{
    public void Fire(Vector2 origin, Vector2 direction, float speed, float damage)
    {
        Debug.Log("Physical Projectile has been thrown");
    }
}