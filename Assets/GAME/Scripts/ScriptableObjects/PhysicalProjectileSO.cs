using UnityEngine;

public class PhysicalProjectile : MonoBehaviour, IProjectile
{
    public void Fire(Vector3 origin, Vector3 direction, float speed, float damage)
    {
        Debug.Log("Physical Projectile has been thrown");
    }
}