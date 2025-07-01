using R3;
using System.Collections.Generic;
using UnityEngine;

public class CharacterHub : MonoBehaviour
{
    [SerializeField] private PlayerInputHandler _inputHandler;
    [SerializeField] private AnimatorSystem _animatorSystem;
    [SerializeField] private MovementManager _movementManager;
    [SerializeField] private CombatManager _combatManager;

    private List<Component> _modules;

    public ActionSystem _actionSystem;
    private Subject<ReactionSnapshot> _dummyReactionSnapshotStream = new();
    private CompositeDisposable _disposables = new();

    private void Awake()
    {
        _actionSystem = new ActionSystem(
            _inputHandler.InputSnapshotStream,
            _movementManager.SnapshotStream,
            _combatManager.SnapshotStream,
            _dummyReactionSnapshotStream,
            new CompositeIntentMapper(new SwordIntentMapper(), new MovementIntentMapper())
            );

        _actionSystem.MovementIntentStream
            .Subscribe(_movementManager.HandleInput)
            .AddTo(_disposables);

        _actionSystem.CombatIntentStream
            .Subscribe(_combatManager.HandleInput)
            .AddTo(_disposables);

        //_movementManager.SnapshotStream.
        //    Subscribe(_ => _actionSystem.ProcessIntent())
        //    .AddTo(_disposables);

        _movementManager.TransitionStream
            .Subscribe(transition =>
            {
                _actionSystem.ProcessIntent();
            });

        _actionSystem.AnimatorActions
            .Subscribe(_animatorSystem.ApplyAnimatorUpdates)
            .AddTo(_disposables);

        _animatorSystem.RootMotionStream
            .Subscribe(_movementManager.HandleRootMotion)
            .AddTo(_disposables);

        _animatorSystem.AnimationFrameStream
            .Subscribe(_combatManager.OnAnimationFrame)
            .AddTo(_disposables);

        _modules = new List<Component>()
        {
            _animatorSystem,
            _movementManager,
            _combatManager
        };
    }
    private void Update()
    {
        _actionSystem.Update();
    }

    private void OnDestroy()
    {
        _disposables.Dispose();
    }
}