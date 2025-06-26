using System;
using UnityEngine;

public class WeaponHitboxSensor : MonoBehaviour
{
    public event Action<Collider> OnHit;

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Enemy")) // Make Better
        {
            OnHit?.Invoke(other);
        }
    }
}