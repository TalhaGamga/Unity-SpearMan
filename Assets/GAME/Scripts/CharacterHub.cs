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
    private Subject<ReactionSnapshot> _dummyReactionStream = new();
    private CompositeDisposable _disposables = new();

    private void Awake()
    {
        _actionSystem = new ActionSystem(
            _inputHandler.InputStream,
            _movementManager.SnapshotStream,
            _combatManager.SnapshotStream,
            _dummyReactionStream,
            new CompositeIntentMapper(new MovementIntentMapper(), new SwordIntentMapper())
            );

        _actionSystem.MovementInputStream
            .Subscribe(_movementManager.HandleInput)
            .AddTo(_disposables);

        _actionSystem.CombatInputStream
            .Subscribe(_combatManager.HandleInput)
            .AddTo(_disposables);

        _actionSystem.AnimatorActions
            .Subscribe(_animatorSystem.HandleInput)
            .AddTo(_disposables);

        _actionSystem.MovementSnapshotStream
            .Subscribe(_animatorSystem.ApplyMovement)
            .AddTo(_disposables);

        _actionSystem.CombatSnapshotStream
            .Subscribe(_animatorSystem.ApplyCombat)
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

    private void OnDestroy()
    {
        _disposables.Dispose();
    }
}