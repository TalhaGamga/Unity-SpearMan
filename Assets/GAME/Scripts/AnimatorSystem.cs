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
    public Observable<CombatAnimationFrame> CombatAnimationFrameStream => _combatAnimationStream;
    public Observable<MovementAnimationFrame> MovementAnimationFrameStream => _movementAnimationStream;

    private readonly Subject<RootMotionFrame> _rootMotionSubject = new();
    private readonly Subject<CombatAnimationFrame> _combatAnimationStream = new();
    private readonly Subject<MovementAnimationFrame> _movementAnimationStream = new();

    // KEY: trigger name, VALUE: frames left before reset
    private Dictionary<string, float> _pendingTriggerResets = new();

    // How many frames to delay trigger reset. 2 is a safe starting value.
    private const float TRIGGER_RESET_TIME = 0.1f;

    void Awake() => _anim = GetComponentInChildren<Animator>();

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
        var parsed = AnimationEventParser.Parse(eventString);
        string system = parsed.TryGetValue("System", out var s) ? s : "";

        if (!string.IsNullOrEmpty(system))
        {
            switch (system)
            {
                case "MovementSystem":
                    //Debug.Log("Movement Animation Frame");
                    var movementFrame = AnimationEventParser.ToMovementAnimationFrame(parsed);
                    _movementAnimationStream.OnNext(movementFrame);
                    break;
                case "CombatSystem":
                    //Debug.Log("Combat Animation Frame");
                    var combatFrame = AnimationEventParser.ToCombatAnimationFrame(parsed);
                    _combatAnimationStream.OnNext(combatFrame);
                    break;
                default:
                    break;
            }
        }
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

    public void ShakeCamera()
    {
        CameraManager.Shake();
    }

    void OnDestroy() => _disposables.Dispose();

    private IEnumerator IESetTrigger(string trigger)
    {
        _anim.SetTrigger(trigger);

        yield return new WaitForSeconds(TRIGGER_RESET_TIME);
        _anim.ResetTrigger(trigger);
    }
}
