using UnityEngine;

public class TestBox : MonoBehaviour, IDamageable, IKnockbackable
{
    private Rigidbody2D rb;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    public void ApplyForce(Vector3 direction)
    {
        rb.AddForce(direction, ForceMode2D.Impulse);
        Debug.Log("Force Applied: " + direction);
    }

    public void ReceiveDamage(float amount)
    {
        Debug.Log("Damage Received");
    }
}