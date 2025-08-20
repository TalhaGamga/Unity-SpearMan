using UnityEngine;
using R3;
using System.Collections.Generic;
using System.Collections;

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

    // KEY: trigger name, VALUE: frames left before reset
    private Dictionary<string, float> _pendingTriggerResets = new();

    // How many frames to delay trigger reset. 2 is a safe starting value.
    private const float TRIGGER_RESET_TIME = 0.05f;

    void Awake() => _anim = GetComponentInChildren<Animator>();

    [SerializeField] bool rootMotion;
    private void Start()
    {
        _anim.applyRootMotion = true;
    }

    private void OnAnimatorMove()
    {
        if (_anim.applyRootMotion)
        {
            var deltaPos = _anim.deltaPosition;
            var deltaRot = _anim.deltaRotation;
            _rootMotionSubject.OnNext(new RootMotionFrame(deltaPos, deltaRot));
        }

        rootMotion = _anim.applyRootMotion;
    }

    private void LateUpdate()
    {
        // Safe: iterate over a copy
        var keys = new List<string>(_pendingTriggerResets.Keys);
        foreach (var trigger in keys)
        {
            _pendingTriggerResets[trigger] -= Time.deltaTime;
            if (_pendingTriggerResets[trigger] <= 0f)
            {
                _anim.ResetTrigger(trigger);
                _pendingTriggerResets.Remove(trigger);
            }
        }
    }

    public void OnAnimationEvent(string eventString)
    {
        int layer = 1;
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
                        //_anim.SetTrigger(update.ParamName);
                        // Start/reset countdown for this trigger
                        //_pendingTriggerResets[update.ParamName] = TRIGGER_RESET_TIME;
                        StartCoroutine(IESetTrigger(update.ParamName));
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

    private IEnumerator IESetTrigger(string trigger)
    {
        _anim.SetTrigger(trigger);

        yield return new WaitForSeconds(TRIGGER_RESET_TIME);
        _anim.ResetTrigger(trigger);
    }
}
