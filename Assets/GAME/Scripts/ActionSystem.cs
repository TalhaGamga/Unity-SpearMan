using R3;
using System.Collections.Generic;
public class ActionSystem
{
    public Subject<MovementAction> MovementIntentStream { get; } = new();
    public Subject<CombatAction> CombatIntentStream { get; } = new();
    public Subject<IEnumerable<AnimatorParamUpdate>> AnimatorUpdateStream { get; } = new();

    private MovementSnapshot _movementSnapshot = MovementSnapshot.Default;
    private CombatSnapshot _combatSnapshot = CombatSnapshot.Default;
    private ReactionSnapshot _reactionSnapshot = ReactionSnapshot.Default;
    private VFXPlaySignal _vfxSnapshot = VFXPlaySignal.Default;
    private InputSnapshot _currentInputSnapshot = InputSnapshot.Empty;

    private readonly CompositeIntentMapper _intentMapper;

    public ActionSystem(
        Observable<InputSnapshot> inputSnapshotStream,
        Observable<MovementSnapshot> movementSnapshotStream,
        Observable<CombatSnapshot> combatSnapshotStream,
        Observable<ReactionSnapshot> reactionSnapshotStream,
        CompositeIntentMapper intentMapper
        )
    {
        _intentMapper = intentMapper;

        inputSnapshotStream.Subscribe(onInputSnapshot);

        movementSnapshotStream.Subscribe(snapshot =>
        {
            _movementSnapshot = snapshot;
        });

        combatSnapshotStream.Subscribe(snapshot =>
        {
            _combatSnapshot = snapshot;
        });

        reactionSnapshotStream.Subscribe(snapshot =>
        {
            _reactionSnapshot = snapshot;
        });
    }
    public void OnInputOrContextChanged()
    {
        ProcessIntent();
    }

    public void ProcessAnimator()
    {
        var characterSnapshot = new CharacterSnapshot(
    _movementSnapshot, _combatSnapshot, _reactionSnapshot);

        var updates = AnimationParameterMapper.AnimatorMapper(characterSnapshot);
        AnimatorUpdateStream.OnNext(updates);
    }

    public void ProcessIntent()
    {
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
        }
    }

    private void onInputSnapshot(InputSnapshot inputSnapshot)
    {
        _currentInputSnapshot = inputSnapshot;
        ProcessIntent();
    }

    // Optionally, you can also have public methods to trigger intent processing
    // e.g., in response to state changes (attack becomes cancelable, etc.),
    // by simply calling ProcessIntent() to always use the latest InputSnapshot.
}
