using R3;

public class ActionSystem
{
    public Subject<MovementAction> MovementInputStream { get; } = new();
    public Subject<CombatAction> CombatInputStream { get; } = new();
    public Subject<AnimatorAction> AnimatorActions { get; } = new();

    public Subject<MovementSnapshot> MovementSnapshotStream { get; } = new();


    private MovementSnapshot _movementSnapshot = MovementSnapshot.Default;
    public CombatSnapshot _combatSnapshot = CombatSnapshot.Default;
    public ReactionSnapshot _reactionSnapshot = ReactionSnapshot.Default;

    private readonly CompositeIntentMapper _intentMapper;

    public ActionSystem(
        Observable<InputType> inputStream,
        Observable<MovementSnapshot> movementStream,
        Observable<CombatSnapshot> combatStream,
        Observable<ReactionSnapshot> reactionStream,
        CompositeIntentMapper intentMapper
        )
    {
        _intentMapper = intentMapper;

        inputStream.Subscribe(OnInput);
        movementStream.Subscribe(snapshot =>
        {
            _movementSnapshot = snapshot;
            MovementSnapshotStream.OnNext(snapshot);
        });
        combatStream.Subscribe(snapshot => _combatSnapshot = snapshot);
        reactionStream.Subscribe(snapshot => _reactionSnapshot = snapshot);
    }

    public void Update()
    {
    }

    public void OnInput(InputType input)
    {
        var snapshot = new CharacterSnapshot(
            _movementSnapshot, _combatSnapshot, _reactionSnapshot
            );

        var intent = _intentMapper.MapInputToIntent(input, snapshot);

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
}