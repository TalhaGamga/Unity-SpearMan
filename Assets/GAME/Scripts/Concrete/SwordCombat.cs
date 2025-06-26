using R3;
using UnityEngine;

public class SwordCombat : ICombat
{
    private readonly Sword _view;
    private BehaviorSubject<CombatSnapshot> _stream { get; }

    private CombatSnapshot _currentSnapshot = CombatSnapshot.Default;
    private bool _canDealDamage = false;

    public SwordCombat(Sword view, BehaviorSubject<CombatSnapshot> stream)
    {
        _view = view;
        _stream = stream;
    }

    public void Init(ICombatManager combatManager)
    {
    }

    public void HandleInput(CombatAction action)
    {
        _currentSnapshot = new CombatSnapshot(action.ActionType, 1f);
        _stream.OnNext(_currentSnapshot);
    }

    public void Update(float deltaTime)
    {
    }

    public void OnWeaponCollision(Collider other)
    {
        // Deal Damage
    }

    public void End()
    {
        _canDealDamage = false;
        _stream.OnNext(CombatSnapshot.Default);
    }
}