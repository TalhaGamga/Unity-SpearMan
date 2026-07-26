using UnityEngine;

[AddComponentMenu("GAME/Reactive Capabilities/Debug Damageable")]
[DisallowMultipleComponent]
public sealed class DebugDamageable : MonoBehaviour, IDamageable
{
    [SerializeField] private bool _logDamage = true;

    public float TotalDamageReceived { get; private set; }

    public void ReceiveDamage(float amount)
    {
        TotalDamageReceived += amount;

        if (!_logDamage)
            return;

        Debug.Log(
            $"{name} received {amount} damage. Total: {TotalDamageReceived}.",
            this
        );
    }
}
