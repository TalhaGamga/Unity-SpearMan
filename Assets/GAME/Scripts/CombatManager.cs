using R3;
using UnityEngine;

public class CombatManager : MonoBehaviour, ICombatManager, ICombatInputReceiver
{
    public BehaviorSubject<CombatSnapshot> SnapshotStream { get; } = new(CombatSnapshot.Default);

    private IWeapon _currentWeapon;
    private ICombat _currentCombat;

    private readonly CompositeDisposable _disposables = new();

    private void Awake()
    {
        SetWeapon(GetComponentInChildren<IWeapon>());
    }

    private void OnDestroy()
    {
        _disposables.Dispose();
        _currentCombat?.End();
    }

    private void Update()
    {
        _currentCombat?.Update(Time.deltaTime);
    }

    public void SetWeapon(IWeapon newWeapon)
    {
        _currentCombat?.End();

        _currentWeapon = newWeapon;
        _currentCombat = newWeapon.CreateCombat(this, SnapshotStream);
    }

    public void HandleInput(CombatAction action)
    {
        _currentCombat?.HandleInput(action);
    }

    public void OnAnimationFrame(AnimationFrame frame)
    {
        _currentCombat?.OnAnimationFrame(frame);
    }
}
