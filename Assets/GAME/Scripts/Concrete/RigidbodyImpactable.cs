using UnityEngine;
using UnityEngine.Serialization;

[AddComponentMenu("GAME/Reactive Capabilities/Rigidbody Impactable")]
[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody))]
public sealed class RigidbodyImpactable : MonoBehaviour, IImpactable
{
    [SerializeField] private Rigidbody _rb;

    [FormerlySerializedAs("_supportedAxes")]
    [SerializeField] private PhysicsAxes _supportedTranslationAxes = PhysicsAxes.XYZ;
    [SerializeField] private PhysicsAxes _supportedRotationAxes = PhysicsAxes.XYZ;

    [SerializeField] private ForceMode _forceMode = ForceMode.Impulse;
    [SerializeField, Min(0f)] private float _maxAngularSpeed = 20f;

    private void Awake()
    {
        if (_rb == null)
            _rb = GetComponent<Rigidbody>();
    }

    public void ApplyImpact(ImpactData impact)
    {
        if (_rb == null)
            return;

        PhysicsAxes translationAxes = PhysicsAxesUtility.Sanitize(
            impact.TranslationAxes & _supportedTranslationAxes
        );
        PhysicsAxes rotationAxes = PhysicsAxesUtility.Sanitize(
            impact.RotationAxes & _supportedRotationAxes
        );
        PhysicsAxesUtility.ApplyConstraints(
            _rb,
            translationAxes,
            rotationAxes
        );
        _rb.maxAngularVelocity = _maxAngularSpeed;

        ApplyLinearImpulse(impact, translationAxes);
        ApplyAngularImpulse(impact, rotationAxes);
    }

    private void ApplyLinearImpulse(
        ImpactData impact,
        PhysicsAxes translationAxes)
    {
        if (impact.Force <= 0f)
            return;

        Vector3 direction = PhysicsAxesUtility.Direction(
            impact.Direction,
            translationAxes
        );

        if (direction == Vector3.zero)
            return;

        _rb.AddForce(direction * impact.Force, _forceMode);
    }

    private void ApplyAngularImpulse(
        ImpactData impact,
        PhysicsAxes rotationAxes)
    {
        if (impact.AngularImpulse <= 0f || impact.Point == Vector3.zero)
            return;

        Vector3 lever = impact.Point - _rb.worldCenterOfMass;
        Vector3 torqueDirection = PhysicsAxesUtility.Project(
            Vector3.Cross(lever, impact.AngularDirection),
            rotationAxes
        );

        if (torqueDirection.sqrMagnitude <= Mathf.Epsilon)
            return;

        _rb.AddTorque(
            torqueDirection.normalized * impact.AngularImpulse,
            _forceMode
        );
    }
}
