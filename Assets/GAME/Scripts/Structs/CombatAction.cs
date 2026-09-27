public struct CombatAction
{
    public CombatType ActionType;

    /// <summary>
    /// Which variant of the attack was asked for. Only read when
    /// <see cref="ActionType"/> is <see cref="CombatType.Stab"/>, which is the
    /// only attack with more than one clip behind it.
    /// </summary>
    public AttackId Attack;
}
