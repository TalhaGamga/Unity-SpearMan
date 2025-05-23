using System;
using UnityEngine;
using R3;

public enum MoveState { Idle, Walk, Run }
public enum CombatState { None, Fire, Reload, Melee }
public enum ReactionState { None, Hit, Dead }

public readonly struct MovementSnapshot
{
    public readonly MoveState State; public readonly float Speed;
    public MovementSnapshot(MoveState state, float speed) { State = state; Speed = speed; }
    public static MovementSnapshot Default => new MovementSnapshot(MoveState.Idle, 0);
}
public readonly struct CombatSnapshot
{
    public readonly CombatState State; public readonly float Energy;
    public CombatSnapshot(CombatState state, float energy) { State = state; Energy = energy; }
    public static CombatSnapshot Default => new CombatSnapshot(CombatState.None, 0);
}
public readonly struct ReactionSnapshot
{
    public readonly ReactionState State; public readonly float Impact;
    public ReactionSnapshot(ReactionState state, float impact) { State = state; Impact = impact; }
    public static ReactionSnapshot Default => new ReactionSnapshot(ReactionState.None, 0);
}

[RequireComponent(typeof(Animator))]
public sealed class AnimatorSystem : MonoBehaviour
{
    [Header("Animator parameter names")]
    [SerializeField] string moveStateParam = "MoveState";
    [SerializeField] string moveSpeedParam = "MoveSpeed";
    [SerializeField] string combatStateParam = "CombatState";
    [SerializeField] string energyParam = "Energy";
    [SerializeField] string isHitParam = "IsHit";

    Animator _anim;
    readonly CompositeDisposable _disposables = new();

    void Awake() => _anim = GetComponent<Animator>();

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

    void ApplyMovement(in MovementSnapshot s)
    {
        Debug.Log(s);
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
        _anim.SetBool(isHitParam, s.State == ReactionState.Hit);
    }

    void OnDestroy() => _disposables.Dispose();
}
