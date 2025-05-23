using UnityEngine;
using DinoFracture;

public class TestBox : MonoBehaviour, IDamageable, IKnockbackable, IDestructible
{
    private Rigidbody _rb;
    [SerializeField] private PreFracturedGeometry _preFractured;
    [SerializeField] private RuntimeFracturedGeometry _runtimeFractured;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();
    }

    public void ApplyForce(Vector3 direction)
    {
        _rb.AddForce(direction, ForceMode.Impulse);
        Debug.Log("Force Applied: " + direction);
    }

    public void ReceiveDamage(float amount)
    {
        Debug.Log("Damage Received");
    }

    public void Break()
    {
        if (_preFractured != null)
        {
            _preFractured.Fracture();
        }

        else if (_runtimeFractured != null)
        {
            _runtimeFractured.Fracture();
        }

        else
        {
            Debug.Log($"DinoDestructible on {gameObject.name} has no fracture component attached.");
        }
    }
}