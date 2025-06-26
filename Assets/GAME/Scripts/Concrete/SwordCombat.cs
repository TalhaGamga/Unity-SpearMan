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
        _currentSnapshot = new CombatSnapshot(action.ActionType, 1f);
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
            if (frame.EventKey == "HitWindowStart" || (frame.Stage == 1 && frame.IsCancelable))
            {
                // Open damage window
                _canDealDamage = true;
                _currentSnapshot = new CombatSnapshot(CombatType.PrimaryAttack, 1f);
                _stream.OnNext(_currentSnapshot);
            }
            else if (frame.EventKey == "HitWindowEnd")
            {
                // Close damage window
                _canDealDamage = false;
            }
            else if (frame.EventKey == "AttackCancelableStart" || frame.IsCancelable)
            {
                // Allow attack cancel (transition to movement or another attack)
                _isCancelable = true;
            }
            else if (frame.EventKey == "AttackCancelableEnd")
            {
                // End cancel window
                _isCancelable = false;
            }
            else if (frame.EventKey == "SlashEnd")
            {
                // End attack, reset state
                _canDealDamage = false;
                _isCancelable = false;
                End();
            }
        }
    }


}