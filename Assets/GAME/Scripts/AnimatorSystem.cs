using UnityEngine;
using R3;
using System.Collections.Generic;

public sealed class AnimatorSystem : MonoBehaviour
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
    public Observable<AnimationFrame> AnimationFrameStream => _animationFrameStream;

    private readonly Subject<RootMotionFrame> _rootMotionSubject = new();
    private readonly Subject<AnimationFrame> _animationFrameStream = new();

    void Awake() => _anim = GetComponentInChildren<Animator>();

    private void Start()
    {
        _anim.applyRootMotion = true;
    }

    public void HandleInput(AnimatorAction action)
    {
        _anim.applyRootMotion = action.UseRootMotion;
        switch (action.ActionType)
        {
            case AnimationType.Idle:
                break;
            case AnimationType.Run:
                break;
            case AnimationType.Jump:
                break;
            default:
                break;
        }
    }

    private void OnAnimatorMove() // Make here hybrid supporting
    {
        if (_anim.applyRootMotion)
        {
            var deltaPos = _anim.deltaPosition;
            var deltaRot = _anim.deltaRotation;

            _rootMotionSubject.OnNext(new RootMotionFrame(deltaPos, deltaRot));
        }
    }

    public void ApplyMovement(MovementSnapshot s)
    {
        var movementUpdates = AnimationParameterMapper.MapMovement(s);
        ApplyAnimatorUpdates(movementUpdates);
    }

    public void ApplyCombat(CombatSnapshot s)
    {
        _anim.SetInteger(combatStateParam, (int)s.State);
    }

    public void OnAnimationEvent(string eventString)
    {
        int layer = 0; // or parse from string as above!
        var frame = AnimationEventParser.ToAnimationFrame(eventString, _anim, layer);
        _animationFrameStream.OnNext(frame);
    }

    public void ApplyAnimatorUpdates(IEnumerable<AnimatorParamUpdate> updates)
    {
        foreach (var update in updates)
        {
            switch (update.ParamType)
            {
                case AnimatorControllerParameterType.Float:
                    _anim.SetFloat(update.ParamName, (float)update.Value);
                    break;
                case AnimatorControllerParameterType.Int:
                    _anim.SetInteger(update.ParamName, (int)update.Value);
                    break;
                case AnimatorControllerParameterType.Bool:
                    _anim.SetBool(update.ParamName, (bool)update.Value);
                    break;
                case AnimatorControllerParameterType.Trigger:
                    _anim.SetTrigger(update.ParamName);
                    break;
            }
        }
    }

    void ApplyReaction(in ReactionSnapshot s)
    {
        _anim.SetBool(isHitParam, s.State == ReactionType.Hit);
    }

    void OnDestroy() => _disposables.Dispose();
}
