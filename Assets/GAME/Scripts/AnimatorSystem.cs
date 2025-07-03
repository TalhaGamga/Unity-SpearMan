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

    private HashSet<string> _triggersToResetNextFrame = new();
    private HashSet<string> _triggersJustSet = new();

    void Awake() => _anim = GetComponentInChildren<Animator>();

    private void Start()
    {
        _anim.applyRootMotion = true;
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

    private void LateUpdate()
    {
        // Actually reset triggers from the *previous* frame
        foreach (var trigger in _triggersToResetNextFrame)
            _anim.ResetTrigger(trigger);

        _triggersToResetNextFrame.Clear();

        // Move just-set triggers into the "reset next frame" set
        var tmp = _triggersToResetNextFrame;
        _triggersToResetNextFrame = _triggersJustSet;
        _triggersJustSet = tmp;
    }

    public void OnAnimationEvent(string eventString)
    {
        int layer = 0; // or parse from string as above!
        var frame = AnimationEventParser.ToAnimationFrame(eventString, _anim, layer);
        _animationFrameStream.OnNext(frame);
    }

    public void HandleAnimatorUpdates(IEnumerable<AnimatorParamUpdate> updates)
    {
        foreach (var update in updates)
        {
            switch (update.ParamType)
            {
                case AnimatorParamUpdateType.Float:
                    _anim.SetFloat(update.ParamName, (float)update.Value);
                    break;
                case AnimatorParamUpdateType.Int:
                    _anim.SetInteger(update.ParamName, (int)update.Value);
                    break;
                case AnimatorParamUpdateType.Bool:
                    _anim.SetBool(update.ParamName, (bool)update.Value);
                    break;
                case AnimatorParamUpdateType.Trigger:
                    if (update.ResetTrigger)
                        _anim.ResetTrigger(update.ParamName);
                    else
                    {
                        _anim.SetTrigger(update.ParamName);
                        _triggersJustSet.Add(update.ParamName);
                    }
                    break;
                case AnimatorParamUpdateType.RootMotion:
                    _anim.applyRootMotion = (bool)update.Value;
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
