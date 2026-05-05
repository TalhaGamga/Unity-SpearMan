using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class RigidbodyImpactable : MonoBehaviour, IImpactable
{
    [SerializeField] private Rigidbody _rb;
    [SerializeField] private ForceMode _forceMode = ForceMode.Impulse;

    private void Awake()
    {
        if (_rb == null)
            _rb = GetComponent<Rigidbody>();
    }

    public void ApplyImpact(ImpactData impact)
    {
        if (_rb == null)
            return;

        Vector3 force = impact.Direction.normalized * impact.Force;

        if (impact.Point != Vector3.zero)
            _rb.AddForceAtPosition(force, impact.Point, _forceMode);
        else
            _rb.AddForce(force, _forceMode);
    }
}