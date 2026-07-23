using System;
using UnityEngine;

public class WeaponHitboxSensor : MonoBehaviour
{
    public event Action<Collider> OnHit;
    public Vector3 Position => _hitboxCenter != null ? _hitboxCenter.position : transform.position;
    public Vector3 BladeDirection => _hitboxCenter != null ? _hitboxCenter.forward : transform.forward;
    public Vector3 Velocity { get; private set; }

    [SerializeField] private Transform _hitboxCenter;
    [SerializeField] private Vector3 _halfExtents = new Vector3(0.5f, 0.5f, 0.5f);
    [SerializeField] private LayerMask _targetLayer;

    private Vector3 _previousPosition;

    private void OnEnable()
    {
        _previousPosition = Position;
        Velocity = Vector3.zero;
    }

    private void LateUpdate()
    {
        Vector3 currentPosition = Position;

        Velocity = Time.deltaTime > Mathf.Epsilon
            ? (currentPosition - _previousPosition) / Time.deltaTime
            : Vector3.zero;

        _previousPosition = currentPosition;
    }

    public Collider[] ScanHits()
    {
        Transform hitbox = _hitboxCenter != null ? _hitboxCenter : transform;

        return Physics.OverlapBox(
            hitbox.position,
            _halfExtents,
            hitbox.rotation,
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