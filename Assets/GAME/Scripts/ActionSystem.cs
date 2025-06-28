using R3;
using UnityEngine;

public class ActionSystem
{
    public Subject<MovementAction> MovementInputStream { get; } = new();
    public Subject<CombatAction> CombatInputStream { get; } = new();
    public Subject<AnimatorAction> AnimatorActions { get; } = new();

    public Subject<MovementSnapshot> MovementSnapshotStream { get; } = new();
    public Subject<CombatSnapshot> CombatSnapshotStream { get; } = new();

    private MovementSnapshot _movementSnapshot = MovementSnapshot.Default;
    private CombatSnapshot _combatSnapshot = CombatSnapshot.Default;
    private ReactionSnapshot _reactionSnapshot = ReactionSnapshot.Default;

    private readonly CompositeIntentMapper _intentMapper;

    // The latest full input snapshot (provided by PlayerInputHandler)
    private InputSnapshot _currentInputSnapshot = InputSnapshot.Empty;

    public ActionSystem(
        Observable<InputSnapshot> inputSnapshotStream,
        Observable<MovementSnapshot> movementStream,
        Observable<CombatSnapshot> combatStream,
        Observable<ReactionSnapshot> reactionStream,
        CompositeIntentMapper intentMapper
        )
    {
        _intentMapper = intentMapper;

        inputSnapshotStream.Subscribe(OnInputSnapshot);

        movementStream.Subscribe(snapshot =>
        {
            _movementSnapshot = snapshot;
            MovementSnapshotStream.OnNext(snapshot);
        });

        combatStream.Subscribe(snapshot =>
        {
            _combatSnapshot = snapshot;
            CombatSnapshotStream.OnNext(snapshot);
        });

        reactionStream.Subscribe(snapshot => _reactionSnapshot = snapshot);
    }

    private void OnInputSnapshot(InputSnapshot inputSnapshot)
    {
        _currentInputSnapshot = inputSnapshot;
        ProcessIntent();
    }

    public void OnInputOrContextChanged()
    {
        ProcessIntent();
    }

    private void ProcessIntent()
    {
        var characterSnapshot = new CharacterSnapshot(
            _movementSnapshot, _combatSnapshot, _reactionSnapshot
        );

        // Map using full current input and character snapshot
        var intent = _intentMapper.MapInputToIntent(_currentInputSnapshot, characterSnapshot);

        if (intent != null)
        {
            if (intent.Value.Movement.HasValue)
                MovementInputStream.OnNext(intent.Value.Movement.Value);
            if (intent.Value.Combat.HasValue)
                CombatInputStream.OnNext(intent.Value.Combat.Value);
            if (intent.Value.Animator.HasValue)
                AnimatorActions.OnNext(intent.Value.Animator.Value);
        }
    }

    // Optionally, you can also have public methods to trigger intent processing
    // e.g., in response to state changes (attack becomes cancelable, etc.),
    // by simply calling ProcessIntent() to always use the latest InputSnapshot.
}
