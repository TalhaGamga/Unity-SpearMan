using System;
using UnityEngine;
using R3;


public sealed class AnimatorSystem : MonoBehaviour, IInitializable<CharacterHub>
{
    [Header("Animator parameter names")]
    [SerializeField] string moveStateParam = "MoveState";
    [SerializeField] string moveSpeedParam = "MoveSpeed";
    [SerializeField] string combatStateParam = "CombatState";
    [SerializeField] string energyParam = "Energy";
    [SerializeField] string isHitParam = "IsHit";

    private Animator _anim;
    private readonly CompositeDisposable _disposables = new();

    public Observable<RootMotionFrame> RootMotionStream => _rootMotionSubject;
    private readonly Subject<RootMotionFrame> _rootMotionSubject = new();

    void Awake() => _anim = GetComponentInChildren<Animator>();

    private void Start()
    {
        _anim.applyRootMotion = true;
    }

    public void RegisterStreams(
        IObservable<MovementSnapshot> moveStream,
        IObservable<CombatSnapshot> combatStream
        /*,IObservable<ReactionSnapshot> reactionStream*/)
    {
        moveStream?
            .ToObservable()
            .Subscribe(mvt => ApplyMovement(mvt))
            .AddTo(_disposables);

        combatStream?
            .ToObservable()
            .Subscribe(cmb => ApplyCombat(cmb))
            .AddTo(_disposables);

        //reactionStream?
        //    .ToObservable()
        //    .Subscribe(rct => ApplyReaction(rct))
        //    .AddTo(_disposables);
    }

    private void OnAnimatorMove() // Make here hybrid supporting
    {
        var deltaPos = _anim.deltaPosition;
        var deltaRot = _anim.deltaRotation;

        _rootMotionSubject.OnNext(new RootMotionFrame(deltaPos, deltaRot));
    }

    void ApplyMovement(in MovementSnapshot s)
    {
        _anim.SetInteger(moveStateParam, (int)s.State);
        _anim.SetFloat(moveSpeedParam, s.Speed);
    }

    void ApplyCombat(in CombatSnapshot s)
    {
        _anim.SetInteger(combatStateParam, (int)s.State);
        _anim.SetFloat(energyParam, s.Energy);
    }

    void ApplyReaction(in ReactionSnapshot s)
    {
        _anim.SetBool(isHitParam, s.State == ReactionType.Hit);
    }

    void OnDestroy() => _disposables.Dispose();

    public void Initialize(CharacterHub hub)
    {
        IObservable<MovementSnapshot> moveStream;
        IObservable<CombatSnapshot> combatStream;

        moveStream = hub.GetModule<MovementManager>().Stream.AsSystemObservable();
        combatStream = hub.GetModule<CombatManager>().Stream.AsSystemObservable();

        moveStream?
            .ToObservable()
            .Subscribe(mvt => ApplyMovement(mvt))
            .AddTo(_disposables);

        combatStream?
            .ToObservable()
            .Subscribe(cmb => ApplyCombat(cmb))
            .AddTo(_disposables);
    }
}
