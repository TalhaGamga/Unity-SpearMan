using UnityEngine;
using R3;
using System.Collections.Generic;
using System.Collections;

public sealed class AnimatorSystem : MonoBehaviour
{
    [Tooltip("Let the ShakeCamera event authored on attack clips shake the " +
        "camera. Off by default: the sword now shakes on confirmed hits, and " +
        "the clip events fire on every swing, so they would shake on whiffs " +
        "too and make a miss read exactly like a hit.")]
    [SerializeField] private bool _shakeFromClipEvents = false;

    private Animator _anim;
    private readonly CompositeDisposable _disposables = new();
    private Coroutine _pendingStateCompletionTrigger;

    public Observable<RootMotionFrame> RootMotionStream => _rootMotionSubject;
    public Observable<CombatAnimationFrame> CombatAnimationFrameStream => _combatAnimationStream;
    public Observable<MovementAnimationFrame> MovementAnimationFrameStream => _movementAnimationStream;

    private readonly Subject<RootMotionFrame> _rootMotionSubject = new();
    private readonly Subject<CombatAnimationFrame> _combatAnimationStream = new();
    private readonly Subject<MovementAnimationFrame> _movementAnimationStream = new();

    private Dictionary<string, float> _pendingTriggerResets = new();

    private const float TRIGGER_RESET_TIME = 0.1f;
    private const float STATE_ENTRY_TIMEOUT = 2f;

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

    public void OnAnimationEvent(AnimationEvent animationEvent)
    {
        if (animationEvent == null)
            return;

        var parsed = AnimationEventParser.Parse(
            animationEvent.stringParameter
        );
        AnimationClip sourceClip = animationEvent.isFiredByAnimator
            ? animationEvent.animatorClipInfo.clip
            : null;
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
                    combatFrame.SourceClip = sourceClip;
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
                // Hashed rather than by name. Note the trade that makes: the
                // string setters log "Parameter does not exist" at run time,
                // while the hash overloads silently do nothing. The loud
                // failure moves to AnimatorContractValidator, which catches
                // the same mistake in the editor rather than in a build.
                case AnimatorParamUpdateType.Float:
                    _anim.SetFloat(update.ParamHash, update.FloatValue);
                    break;
                case AnimatorParamUpdateType.Int:
                    _anim.SetInteger(update.ParamHash, update.IntValue);
                    break;
                case AnimatorParamUpdateType.Bool:
                    _anim.SetBool(update.ParamHash, update.BoolValue);
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
                    _anim.applyRootMotion = update.BoolValue;
                    break;
            }
        }
    }

    /// <summary>
    /// Called by name from the attack clips' own animation events, which sit
    /// in third-party clips and fire whether or not the swing connected.
    /// Kept as a receiver - removing it would make every such event log a
    /// missing-method error - but silent unless explicitly re-enabled, since
    /// the weapon now shakes the camera itself on the first confirmed hit.
    /// </summary>
    public void ShakeCamera()
    {
        if (!_shakeFromClipEvents)
            return;

        CameraManager.Shake();
    }

    public void TriggerAfterStateCompletes(
        string stateName,
        string triggerName)
    {
        if (string.IsNullOrWhiteSpace(stateName) ||
            string.IsNullOrWhiteSpace(triggerName))
        {
            Debug.LogWarning(
                $"{name}: Cannot schedule an Animator trigger without " +
                "both a state and trigger name.",
                this
            );
            return;
        }

        CancelPendingStateCompletionTrigger();
        _pendingStateCompletionTrigger = StartCoroutine(
            IETriggerAfterStateCompletes(stateName, triggerName)
        );
    }

    public void CancelPendingStateCompletionTrigger()
    {
        if (_pendingStateCompletionTrigger == null)
            return;

        StopCoroutine(_pendingStateCompletionTrigger);
        _pendingStateCompletionTrigger = null;
    }

    private void OnDestroy()
    {
        CancelPendingStateCompletionTrigger();
        _disposables.Dispose();
    }

    private IEnumerator IETriggerAfterStateCompletes(
        string stateName,
        string triggerName)
    {
        int stateHash = Animator.StringToHash(stateName);
        float entryDeadline = Time.time + STATE_ENTRY_TIMEOUT;
        int layerIndex = -1;

        while (Time.time <= entryDeadline)
        {
            layerIndex = FindCurrentStateLayer(stateHash);
            if (layerIndex >= 0)
                break;

            yield return null;
        }

        if (layerIndex < 0)
        {
            Debug.LogWarning(
                $"{name}: Animator state '{stateName}' was not entered " +
                $"within {STATE_ENTRY_TIMEOUT:0.##} seconds.",
                this
            );
            _pendingStateCompletionTrigger = null;
            yield break;
        }

        while (true)
        {
            AnimatorStateInfo stateInfo =
                _anim.GetCurrentAnimatorStateInfo(layerIndex);

            if (stateInfo.shortNameHash != stateHash)
            {
                _pendingStateCompletionTrigger = null;
                yield break;
            }

            if (stateInfo.normalizedTime >= 1f &&
                !_anim.IsInTransition(layerIndex))
            {
                break;
            }

            yield return null;
        }

        _pendingStateCompletionTrigger = null;
        StartCoroutine(IESetTrigger(triggerName));
    }

    private int FindCurrentStateLayer(int stateHash)
    {
        for (int layerIndex = 0;
            layerIndex < _anim.layerCount;
            layerIndex++)
        {
            AnimatorStateInfo stateInfo =
                _anim.GetCurrentAnimatorStateInfo(layerIndex);

            if (stateInfo.shortNameHash == stateHash)
                return layerIndex;
        }

        return -1;
    }

    private IEnumerator IESetTrigger(string trigger)
    {
        _anim.SetTrigger(trigger);

        yield return new WaitForSeconds(TRIGGER_RESET_TIME);
        _anim.ResetTrigger(trigger);
    }
}
