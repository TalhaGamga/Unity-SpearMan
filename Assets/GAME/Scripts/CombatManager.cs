using System;
using R3;
using UnityEngine;

public class CombatManager : MonoBehaviour, ICombatManager
{
    public Subject<CombatSnapshot> SnapshotStream { get; } = new();

    /// <summary>
    /// Effect requests from whichever weapon is equipped right now.
    ///
    /// Multiplexed here so the rest of the character wires to one stable stream
    /// and never has to notice that weapons come and go.
    /// </summary>
    public Subject<VFXPlaySignal> VFXPlayStream { get; } = new();

    public Subject<CombatTransition> TransitionStream { get; } = new();

    private IWeapon _currentWeapon;
    private ICombat _currentCombat;
    private IDisposable _weaponVisuals;

    private readonly CompositeDisposable _disposables = new();
    [SerializeField] private CombatType _currentType;

    private void Awake()
    {
        IWeapon weapon = GetComponentInChildren<IWeapon>();

        if (weapon == null)
        {
            this.enabled = false;
            return;
        }

        SetWeapon(GetComponentInChildren<IWeapon>());

    }

    private void OnDestroy()
    {
        _weaponVisuals?.Dispose();
        _disposables.Dispose();
        _currentCombat?.End();
    }

    private void Update()
    {
        _currentCombat?.Update(Time.deltaTime);
        _currentType = _currentCombat.CombatType;
    }

    public void SetWeapon(IWeapon newWeapon)
    {
        _currentCombat?.End();
        _weaponVisuals?.Dispose();

        _currentWeapon = newWeapon;
        _currentCombat = newWeapon.CreateCombat(this);
        _currentCombat.Init(this, SnapshotStream, TransitionStream);

        // Re-point the visual feed at the new weapon. Subscribers upstream keep
        // their subscription to VFXPlayStream and never see the swap.
        _weaponVisuals = newWeapon.VisualPlayStream?
            .Subscribe(VFXPlayStream.OnNext);
    }

    public void HandleAction(CombatAction action)
    {
        _currentCombat?.HandleAction(action);
    }

    /// <summary>
    /// Both the combat logic and the weapon read the same frame, for unrelated
    /// reasons: one opens hit windows, the other decides whether anything
    /// should be visible. Neither knows about the other.
    /// </summary>
    public void OnAnimationFrame(CombatAnimationFrame frame)
    {
        _currentCombat?.OnAnimationFrame(frame);
        _currentWeapon?.OnAnimationFrame(frame);
    }
}
