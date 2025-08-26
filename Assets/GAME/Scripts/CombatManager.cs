using R3;
using UnityEngine;

public class CombatManager : MonoBehaviour, ICombatManager, ICombatInputReceiver
{
    public BehaviorSubject<CombatSnapshot> SnapshotStream { get; } = new(CombatSnapshot.Default);
    public BehaviorSubject<CombatTransition> TransitionStream { get; } = new(new CombatTransition { From = CombatType.Idle, To = CombatType.Idle });

    private IWeapon _currentWeapon;
    private ICombat _currentCombat;

    private readonly CompositeDisposable _disposables = new();
    [SerializeField] private CombatType _currentType;

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
        _currentType = _currentCombat.CombatType;
    }

    public void SetWeapon(IWeapon newWeapon)
    {
        _currentCombat?.End();

        _currentWeapon = newWeapon;
        _currentCombat = newWeapon.CreateCombat(this, SnapshotStream, TransitionStream);
    }

    public void HandleInput(CombatAction action)
    {
        _currentCombat?.HandleInput(action);
    }

    public void OnAnimationFrame(CombatAnimationFrame frame)
    {
        _currentCombat?.OnAnimationFrame(frame);
    }
}
