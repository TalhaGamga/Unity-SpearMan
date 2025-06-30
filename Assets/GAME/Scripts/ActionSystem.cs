using R3;
using System.Collections.Generic;
using UnityEngine;

public class ActionSystem
{
    public Subject<MovementAction> MovementIntentStream { get; } = new();
    public Subject<CombatAction> CombatIntentStream { get; } = new();
    public Subject<IEnumerable<AnimatorParamUpdate>> AnimatorActions { get; } = new();

    private MovementSnapshot _movementSnapshot = MovementSnapshot.Default;
    private CombatSnapshot _combatSnapshot = CombatSnapshot.Default;
    private ReactionSnapshot _reactionSnapshot = ReactionSnapshot.Default;

    private readonly CompositeIntentMapper _intentMapper;

    // The latest full input snapshot (provided by PlayerInputHandler)
    private InputSnapshot _currentInputSnapshot = InputSnapshot.Empty;

    public ActionSystem(
        Observable<InputSnapshot> inputSnapshotStream,
        Observable<MovementSnapshot> movementSnapshotStream,
        Observable<CombatSnapshot> combatSnapshotStream,
        Observable<ReactionSnapshot> reactionSnapshotStream,
        CompositeIntentMapper intentMapper
        )
    {
        _intentMapper = intentMapper;

        inputSnapshotStream.Subscribe(OnInputSnapshot);

        movementSnapshotStream.Subscribe(snapshot =>
        {
            _movementSnapshot = snapshot;
        });

        combatSnapshotStream.Subscribe(snapshot =>
        {
            _combatSnapshot = snapshot;
        });

        reactionSnapshotStream.Subscribe(snapshot => _reactionSnapshot = snapshot);
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

    public void ProcessIntent()
    {
        Debug.Log("Process Intent");
        var characterSnapshot = new CharacterSnapshot(
            _movementSnapshot, _combatSnapshot, _reactionSnapshot
        );

        // Map using full current input and character snapshot
        var intent = _intentMapper.MapInputToIntent(_currentInputSnapshot, characterSnapshot);

        if (intent != null)
        {
            if (intent.Value.Movement.HasValue)
                MovementIntentStream.OnNext(intent.Value.Movement.Value);
            if (intent.Value.Combat.HasValue)
                CombatIntentStream.OnNext(intent.Value.Combat.Value);
            if (intent.Value.AnimatorUpdates != null)
                AnimatorActions.OnNext(intent.Value.AnimatorUpdates);
        }
    }

    // Optionally, you can also have public methods to trigger intent processing
    // e.g., in response to state changes (attack becomes cancelable, etc.),
    // by simply calling ProcessIntent() to always use the latest InputSnapshot.
}
