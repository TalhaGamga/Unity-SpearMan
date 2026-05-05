using System;
using UnityEngine;

public class WeaponHitboxSensor : MonoBehaviour
{
    public event Action<Collider> OnHit;

    [SerializeField] private Transform _hitboxCenter;
    [SerializeField] private Vector3 _halfExtents = new Vector3(0.5f, 0.5f, 0.5f);
    [SerializeField] private LayerMask _targetLayer;

    public Collider[] ScanHits()
    {
        return Physics.OverlapBox(
            _hitboxCenter.position,
            _halfExtents,
            _hitboxCenter.rotation,
            _targetLayer
        );
    }

    private void OnTriggerEnter(Collider other)
    {
        OnHit?.Invoke(other);
    }

    private void OnDrawGizmosSelected()
    {
        if (_hitboxCenter == null)
            return;

        Gizmos.color = Color.red;

        Matrix4x4 prevMatrix = Gizmos.matrix;

        Gizmos.matrix = Matrix4x4.TRS(
            _hitboxCenter.position,
            _hitboxCenter.rotation,
            Vector3.one
        );

        Gizmos.DrawWireCube(Vector3.zero, _halfExtents * 2f);

        Gizmos.matrix = prevMatrix;
    }
}