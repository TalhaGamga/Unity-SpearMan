public sealed class DamageEvent : IDamageEvent
{
    private readonly float _amount;
    public DamageEvent(float amount) => _amount = amount;

    public void Consume(TargetContext ctx) => ctx.Damageable?.ReceiveDamage(_amount);
}