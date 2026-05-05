public sealed class DamageEvent : IReactiveEvent
{
    private readonly float _damage;

    public DamageEvent(float damage)
    {
        _damage = damage;
    }

    public void Consume(TargetContext ctx)
    {
        ctx.Damageable?.ReceiveDamage(_damage);
    }
}