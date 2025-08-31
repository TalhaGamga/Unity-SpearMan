using R3;
using System;
using UnityEngine;

public class SwordCombat : ICombat
{
    public CombatType CombatType => _currentSnapshot.CurrentState;

    private readonly Sword _view;
    private Subject<CombatSnapshot> _stream = new();
    private Subject<CombatTransition> _transitionStream = new();

    private CombatSnapshot _currentSnapshot = CombatSnapshot.Default;

    private int _currentComboStep = 0;       // 0 = not attacking, 1+ = current step
    private bool _canCombo = false;
    private bool _comboQueued = false;
    private readonly int _maxCombo = 3;

    private bool _canDealDamage = false;

    public SwordCombat(
        Sword view)
    {
        _view = view;
    }

    public void HandleInput(CombatAction action)
    {
        if (action.ActionType == CombatType.GroundedPrimaryAttack)
        {
            // If not in combo, start from step 1
            if (_currentComboStep == 0)
            {
                _currentComboStep = 1;
                _currentSnapshot = new CombatSnapshot(
                    state: CombatType.GroundedPrimaryAttack,
                    isCancelable: false,
                    comboStep: _currentComboStep,
                    triggerAttack: true
                );
                _stream.OnNext(_currentSnapshot);
            }
            else if (_canCombo && _currentComboStep < _maxCombo)
            {
                // Combo window open, advance to next step
                _currentComboStep++;
                _currentSnapshot = new CombatSnapshot(
                    state: CombatType.GroundedPrimaryAttack,
                    isCancelable: false,
                    comboStep: _currentComboStep,
                    triggerAttack: true
                );
                _canCombo = false;
                _stream.OnNext(_currentSnapshot);
            }
            else
            {
                // Buffer attack for next combo window
                _comboQueued = true;
            }
        }
        else if (action.ActionType == CombatType.Cancel)
        {
            ResetCombat();
        }
    }

    public void OnAnimationFrame(CombatAnimationFrame frame)
    {
        // Damage windows
        if (frame.EventKey == "HitWindowStart")
        {
            _canDealDamage = true;
        }
        else if (frame.EventKey == "HitWindowEnd")
        {
            _canDealDamage = false;
        }

        // Combo windows
        if (frame.EventKey == "ComboWindowOpen")
        {
            _canCombo = true;
            if (_comboQueued && _currentComboStep < _maxCombo)
            {
                _comboQueued = false;
                _currentComboStep++;
                // Issue next step snapshot; will trigger animator update for the next attack
                _currentSnapshot = new CombatSnapshot(
                    state: CombatType.GroundedPrimaryAttack,
                    isCancelable: false,
                    comboStep: _currentComboStep,
                    triggerAttack: true
                );
                _canCombo = false;
                _stream.OnNext(_currentSnapshot);
            }
        }
        else if (frame.EventKey == "ComboWindowClose")
        {
            _canCombo = false;
        }

        // Phase/state transitions (if you want to track these)
        if (frame.EventKey == "SlashStart")
        {
            _currentSnapshot = new CombatSnapshot(
                state: CombatType.InPrimaryAttack,
                isCancelable: frame.IsCancelable,
                comboStep: _currentComboStep
            );
            _stream.OnNext(_currentSnapshot);
            _transitionStream.OnNext(new CombatTransition
            {
                From = CombatType.GroundedPrimaryAttack,
                To = CombatType.InPrimaryAttack
            });
        }
        else if (frame.EventKey == "SlashEnd" || frame.EventKey == "ComboAttackEnd")
        {
            ResetCombat();
            _transitionStream.OnNext(new CombatTransition
            {
                From = CombatType.InPrimaryAttack,
                To = CombatType.Idle
            });
        }
    }

    public void OnWeaponCollision(Collider other)
    {
        if (_canDealDamage)
        {
            // Deal damage to target
        }
    }

    public void Update(float deltaTime) { /* ...optional logic... */ }

    public void End() => ResetCombat();

    private void ResetCombat()
    {
        _canDealDamage = false;
        _currentComboStep = 0;
        _canCombo = false;
        _comboQueued = false;
        _currentSnapshot = CombatSnapshot.Default;
        _stream.OnNext(_currentSnapshot);
    }

    public void Init(ICombatManager combatManager, Subject<CombatSnapshot> stream, Subject<CombatTransition> transitionStream)
    {
        _stream = stream;
        _transitionStream = transitionStream;
    }
}
