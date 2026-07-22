using UnityEngine;
using DinoFracture;

public class TestBox : MonoBehaviour, IDamageable, IImpactable, IDestructible
{
    private Rigidbody _rb;
    //[SerializeField] private PreFracturedGeometry _preFractured;
    //[SerializeField] private RuntimeFracturedGeometry _runtimeFractured;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();
    }
    public void ReceiveDamage(float amount)
    {
        Debug.Log("Damage Received");
    }

    public void Destruct(DestructData data)
    {
        //if (_preFractured != null)
        //{
        //    _preFractured.Fracture();
        //}

        //else if (_runtimeFractured != null)
        //{
        //    _runtimeFractured.Fracture();
        //}

        //else
        //{
        //    Debug.Log($"DinoDestructible on {gameObject.name} has no fracture component attached.");
        //}
    }

    public void ApplyImpact(ImpactData data)
    {
        if (_rb == null)
            return;

        Vector3 force = data.Direction.normalized * data.Force;

        if (data.Point != Vector3.zero)
            _rb.AddForceAtPosition(force, data.Point, ForceMode.Impulse);
        else
            _rb.AddForce(force, ForceMode.Impulse);
    }
}