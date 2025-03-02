using UnityEngine;

[CreateAssetMenu(menuName = "ScriptableObjects/Weapon/Firearm/HitscanProjectile")]
public class HitscanProjectile : ProjectileBaseSO
{
    [SerializeField] private float range = 100f;
    public override void Fire(Vector3 origin, Vector3 direction, float speed, float damage)
    {
        RaycastHit2D hit = Physics2D.Raycast(origin, direction, range);
        if (hit.collider != null)
        {
            Debug.Log($"Hitscan Hit: {hit.collider.gameObject.name}");
        }
    }
}