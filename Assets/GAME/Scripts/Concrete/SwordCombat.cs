using R3;
using UnityEngine;

public class SwordCombat : ICombat
{
    private readonly Sword _view;
    private BehaviorSubject<CombatSnapshot> _stream { get; }

    private CombatSnapshot _currentSnapshot = CombatSnapshot.Default;
    private bool _canDealDamage = false;
    private bool _isCancelable;

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
        #region
        _currentSnapshot = new CombatSnapshot(action.ActionType, 1f, false);
        _stream.OnNext(_currentSnapshot);
        #endregion

        // Handle Input as in the RbMober 
    }

    public void Update(float deltaTime)
    {
        // Handle Update if needed
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

    public void OnAnimationFrame(AnimationFrame frame) // Continue to cancelability
    {
        // Example: handle attack action and hit/cancel logic by event
        if (frame.ActionType == "Slash")
        {
            _stream.OnNext(_currentSnapshot);
            _currentSnapshot = new CombatSnapshot(CombatType.InPrimaryAttack, 1f, frame.IsCancelable);
            _stream.OnNext(_currentSnapshot);
            //Debug.Log("In Primary Attack Called");
        }

        if (frame.ActionType == "SlashEnd")
        {
            _currentSnapshot = new CombatSnapshot(CombatType.Idle, 1f, frame.IsCancelable);
            _stream.OnNext(_currentSnapshot);
            //Debug.Log("End Primary Attack Called");
        }
    }
}