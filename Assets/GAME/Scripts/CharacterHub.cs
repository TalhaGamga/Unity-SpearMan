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

        _inputHandler.MovementDirectionStream
            .Subscribe(_movementManager.SetMoveInput)
            .AddTo(_disposables);

        _animatorSystem.RootMotionStream
            .Subscribe(_movementManager.HandleRootMotion)
            .AddTo(_disposables);

        _modules = new List<Component>()
        {
            _animatorSystem,
            _movementManager,
            _combatManager
        };
    }

    private void Start()
    {
        //foreach (var module in _modules)
        //{
        //    if (module is IInitializable<CharacterHub> initializable)
        //    {
        //        initializable.Initialize(this);
        //    }
        //}
    }

    private void Update()
    {
    }



    public T GetModule<T>() where T : class
    {
        foreach (var m in _modules)
        {
            if (m is T t) return t;
        }

        return null;
    }

    private void OnDestroy()
    {
        _disposables.Dispose();
    }
}