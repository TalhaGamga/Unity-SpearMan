using R3;
using UnityEngine;

public class SwordCombat : ICombat
{
    private readonly Sword _view;
    private readonly BehaviorSubject<CombatSnapshot> _stream;
    private CombatSnapshot _currentSnapshot = CombatSnapshot.Default;
    private bool _canDealDamage = false;

    public SwordCombat(Sword view, BehaviorSubject<CombatSnapshot> stream)
    {
        _view = view;
        _stream = stream;
    }

    public void Init(ICombatManager combatManager)
    {
        // (Optional) Set up references if needed
    }

    public void HandleInput(CombatAction action)
    {
        // Only trigger attack on explicit input for eventful actions
        if (action.ActionType == CombatType.PrimaryAttack)
        {
            _currentSnapshot = new CombatSnapshot(
                state: CombatType.PrimaryAttack,
                energy: 1f,
                isCancelable: false,
                triggerAttack: true  // <-- Only true for this frame!
            );
            _stream.OnNext(_currentSnapshot);

            // Immediately clear trigger in the next frame/snapshot to ensure it's one-shot
            // This prevents re-firing unless a new attack input comes in
            _view.StartCoroutine(ResetAttackTriggerNextFrame());
        }
        // You can add parry or other combat actions similarly...
    }

    private System.Collections.IEnumerator ResetAttackTriggerNextFrame()
    {
        yield return null; // wait one frame
        _currentSnapshot = new CombatSnapshot(
            state: _currentSnapshot.State,
            energy: _currentSnapshot.Energy,
            isCancelable: _currentSnapshot.IsCancelable,
            resetAttackTrigger: true
        );
        _stream.OnNext(_currentSnapshot);
    }

    public void Update(float deltaTime)
    {
        // Handle per-frame combat logic if needed (e.g., charge attack, combo windows, etc)
    }

    public void OnWeaponCollision(Collider other)
    {
        if (_canDealDamage)
        {
            // Deal damage to target
        }
    }

    public void End()
    {
        _canDealDamage = false;
        _currentSnapshot = CombatSnapshot.Default;
        _stream.OnNext(_currentSnapshot);
    }

    public void OnAnimationFrame(AnimationFrame frame)
    {
        Debug.Log("On Animation Frame");
        // Handle animation-driven state transitions
        switch (frame.ActionType)
        {
            case "Slash":
                _canDealDamage = true;
                _currentSnapshot = new CombatSnapshot(
                    state: CombatType.InPrimaryAttack,
                    energy: 1f,
                    isCancelable: frame.IsCancelable,
                    triggerAttack: false // Not a trigger here; only in HandleInput
                );
                _stream.OnNext(_currentSnapshot);
                break;

            case "SlashEnd":
                _canDealDamage = false;
                _currentSnapshot = new CombatSnapshot(
                    state: CombatType.Idle,
                    energy: 1f,
                    isCancelable: frame.IsCancelable,
                    triggerAttack: false
                );
                _stream.OnNext(_currentSnapshot);
                break;

                // Add cases for parry, combo, etc if needed
        }
    }
}
