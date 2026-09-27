using DevVorpian;
using Movement.State;
using R3;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Combat
{
    [System.Serializable]
    public class SwordCombatMachine : ICombat
    {
        public CombatType CombatType => _currentCombatType;

        [SerializeField] private Context _context;
        [SerializeField] private StateMachine<CombatType> _stateMachine;
        [SerializeField] private AttackDatabase _attackDatabase;
        [SerializeField] private ComboAttackKey[] _comboAttackKeys;

        private AttackDefinition _activeAttack;
        private bool _canDealDamage;
        private readonly HashSet<GameObject> _hitTargets = new();
        private bool _isHitWindowOpen;
        private string _activeHitWindowStateName;
        private int _activeHitWindowComboStep;
        private Dictionary<int, string> _comboAttackLookup;
        private Dictionary<AnimationClip, int> _comboStepByClip;
        private Dictionary<int, AnimationClip> _comboClipByStep;
        private Dictionary<int, string> _comboStateNameLookup;

        /// <summary>
        /// How much longer than its own clip an attack may run before the
        /// machine stops believing the animation is going to end it.
        /// </summary>
        private const float AttackOverrunFactor = 1.25f;
        private const float AttackOverrunGrace = 0.35f;

        /// <summary>Ceiling for an attack with no clip to measure against.</summary>
        private const float AttackOverrunFallback = 3f;

        /// <summary>Seconds left before the current attack is ended by force.</summary>
        private float _attackWatchdog;

        private Sword _view;
        private readonly Subject<Unit> _snapshotStreamer = new();
        private readonly BehaviorSubject<CombatType> _transitionStreamer = new(CombatType.Idle);

        private CompositeDisposable _disposables = new();
        private CombatSnapshot _currentSnapshot = CombatSnapshot.Default;
        private CombatType _currentCombatType;

        public void SetSwordView(Sword view)
        {
            _view = view;
        }

        public void Init(ICombatManager combatManager, Subject<CombatSnapshot> snapshotStream, Subject<CombatTransition> transitionStream)
        {
            _stateMachine = new StateMachine<CombatType>();

            _snapshotStreamer
                .Select(_ => new CombatSnapshot(_context.State, _context.Attack, _context.IsCancelable, _context.ComboStep, _context.IsAttacking, resolveLocomotion()))
                .DistinctUntilChanged()
                .Subscribe(snapshotStream.OnNext)
                .AddTo(_disposables);

            _transitionStreamer
                .Pairwise()
                .Subscribe(pair =>
                {
                    transitionStream.OnNext(new CombatTransition(pair.Previous, pair.Current));
                })
                .AddTo(_disposables);

            _stateMachine.OnTransitionedAutonomously.AddListener(submitTransitionStream);

            var idleState = new ConcreteState("Idle");
            var grPA_S1 = new ConcreteState("GrPA_S1");
            var grPA_S2 = new ConcreteState("GrPA_S2");
            var grPA_S3 = new ConcreteState("GrPA_S3");
            var stab = new ConcreteState("DashingAttack");

            _comboStateNameLookup = new Dictionary<int, string>
            {
                [1] = grPA_S1.StateName,
                [2] = grPA_S2.StateName,
                [3] = grPA_S3.StateName
            };

            #region OnEnter
            idleState.OnEnter.AddListener(() =>
            {
                resetHitFrame();
                setContextState(CombatType.Idle);
                setAttackSequence(false, 0, AttackId.None);
                disarmAttackWatchdog();
                setCanCombo(false);
                setCancelable(false);

                submitSnapshot();
            });

            grPA_S1.OnEnter.AddListener(() =>
            {
                setContextState(CombatType.GroundedPrimaryAttack);
                setAttackSequence(true, 1, AttackId.ComboOpener);
                armAttackWatchdog(1);
                setCanCombo(false);
                setCancelable(false);

                submitSnapshot();
            });

            grPA_S2.OnEnter.AddListener(() =>
            {
                setContextState(CombatType.GroundedPrimaryAttack);
                setAttackSequence(true, 2, AttackId.ComboFollow);
                armAttackWatchdog(2);
                setCanCombo(false);
                setCancelable(false);

                submitSnapshot();
            });

            grPA_S3.OnEnter.AddListener(() =>
            {
                setContextState(CombatType.GroundedPrimaryAttack);
                setAttackSequence(true, 3, AttackId.ComboFinisher);
                armAttackWatchdog(3);
                setCanCombo(false);
                setCancelable(false);

                submitSnapshot();
            });

            stab.OnEnter.AddListener(() =>
            {
                setContextState(CombatType.Stab);
                setAttackSequence(true, 0, _context.RequestedStab);
                armAttackWatchdog(0);
                setCanCombo(false);
                setCancelable(false);

                submitSnapshot();
            });
            #endregion

            #region OnExit
            grPA_S1.OnExit.AddListener(() =>
            {
                closeHitFrameForState(grPA_S1.StateName);
                setCanCombo(false);
                setCancelable(false);

                submitSnapshot();
            });

            grPA_S2.OnExit.AddListener(() =>
            {
                closeHitFrameForState(grPA_S2.StateName);
                setCanCombo(false);
                setCancelable(false);

                submitSnapshot();
            });

            grPA_S3.OnExit.AddListener(() =>
            {
                closeHitFrameForState(grPA_S3.StateName);
                setCanCombo(false);
                setCancelable(false);

                submitSnapshot();
            });

            stab.OnExit.AddListener(() =>
            {
                closeHitFrameForState(stab.StateName);
                setCanCombo(false);
                setCancelable(false);
                resetRequestedStab();

                submitSnapshot();
            });
            #endregion

            var initialIdle = new StateTransition<CombatType>(null, idleState, CombatType.Idle, onTransition: () => Debug.Log("Transitioning to Idle"));
            var idleToGrPA_S1 = new StateTransition<CombatType>(idleState, grPA_S1, CombatType.GroundedPrimaryAttack, onTransition: () => Debug.Log("Transitioning GrPrimaryAttack from Idle"));
            var grPA_S1ToS2 = new StateTransition<CombatType>(grPA_S1, grPA_S2, CombatType.GroundedPrimaryAttack, condition: () => _context.CanCombo, onTransition: () => Debug.Log("Transitioning GrPrimaryAttackCS2 from GrPCS1"));
            var grPA_S2ToS3 = new StateTransition<CombatType>(grPA_S2, grPA_S3, CombatType.GroundedPrimaryAttack, condition: () => _context.CanCombo, onTransition: () => Debug.Log("Transitioning GrPrimaryAttackCS3 from GrPC2"));
            var grPA_S3ToS1 = new StateTransition<CombatType>(grPA_S3, grPA_S1, CombatType.GroundedPrimaryAttack, condition: () => _context.CanCombo, onTransition: () => Debug.Log("Transitioning GrPrimaryAttackCS1 from GrPC3"));
            var attackToIdle = new StateTransition<CombatType>(null, idleState, CombatType.Idle, () => !_context.IsAttacking, () => Debug.Log("Transitioning to Idle On Attack End"));

            var toDashingAttack = new StateTransition<CombatType>(null, stab, CombatType.Stab, onTransition: () =>
            {
                Debug.Log("Transitioning to Dashing Attack");
            });

            var dashingAttackToGrPA_S1 = new StateTransition<CombatType>(stab, grPA_S1, CombatType.GroundedPrimaryAttack, condition: () => _context.CanCombo, onTransition: () => Debug.Log("Transitioning to GrPrimaryAttack from dashingAttack"));

            _stateMachine.AddIntentBasedTransition(initialIdle);
            _stateMachine.AddIntentBasedTransition(idleToGrPA_S1);
            _stateMachine.AddIntentBasedTransition(grPA_S1ToS2);
            _stateMachine.AddIntentBasedTransition(grPA_S2ToS3);
            _stateMachine.AddIntentBasedTransition(grPA_S3ToS1);

            _stateMachine.AddIntentBasedTransition(toDashingAttack);
            _stateMachine.AddIntentBasedTransition(dashingAttackToGrPA_S1);

            _stateMachine.AddAutonomicTransition(attackToIdle);

            _stateMachine.SetState(CombatType.Idle);
        }

        public void HandleAction(CombatAction action)
        {
            if (action.ActionType == CombatType.GroundedPrimaryAttack &&
                _context.IsAttacking &&
                !_context.CanCombo)
            {
                return;
            }

            // Below the reject guard on purpose, and scoped to the one action
            // that carries a variant. It used to run first, so a ground attack
            // the machine was about to throw away still reached in and
            // overwrote the selector on its way out - an animator-facing value
            // mutated by an action that never happened.
            if (action.ActionType == CombatType.Stab)
                _context.RequestedStab = action.Attack;

            _stateMachine.SetState(action.ActionType);
        }

        public void OnAnimationFrame(CombatAnimationFrame frame)
        {
            HitWindowIdentity hitWindow = default;
            bool isHitWindowEvent =
                frame.EventKey == "HitFrameOpen" ||
                frame.EventKey == "HitFrameClose";

            if (isHitWindowEvent)
            {
                if (!tryResolveHitWindowIdentity(frame, out hitWindow) ||
                    !isCurrentState(hitWindow.StateName))
                {
                    return;
                }
            }
            else if (!isCurrentState(frame.StateName))
            {
                return;
            }

            switch (frame.EventKey)
            {
                case "HitFrameOpen":
                    openHitFrame(hitWindow);
                    break;

                case "HitFrameClose":
                    closeHitFrame(hitWindow);
                    break;

                case "Cancelable":
                    setCancelable(true);
                    submitSnapshot();
                    submitTransitionStream();
                    break;

                case "ComboWindowOpen":
                    setCanCombo(true);
                    break;

                case "ComboWindowClose":
                    setCanCombo(false);
                    break;

                case "SlashEnd":
                    setAttackSequence(false);
                    setCanCombo(false);
                    closeHitFrameForState(frame.StateName);
                    disarmAttackWatchdog();
                    break;
            }
        }

        public void OnWeaponCollision(Collider other)
        {
        }

        public void Update(float deltaTime)
        {
            // Before the machine steps, so an attack the watchdog has just
            // given up on leaves for Idle on this frame rather than the next.
            tickAttackWatchdog(deltaTime);

            _stateMachine.Update();

            if (_isHitWindowOpen && _activeAttack != null)
            {
                processHitFrame();
            }
        }

        public void End()
        {
            resetHitFrame();
        }

        private void openHitFrame(HitWindowIdentity hitWindow)
        {
            if (!tryResolveAttackDefinition(
                hitWindow.ComboStep,
                out AttackDefinition attack))
            {
                return;
            }

            resetHitFrame();
            _activeAttack = attack;
            _activeHitWindowStateName = hitWindow.StateName;
            _activeHitWindowComboStep = hitWindow.ComboStep;
            _isHitWindowOpen = true;

            if (_view != null)
                _view.OnHitWindowChanged(true, _activeHitWindowStateName);
        }

        private void closeHitFrame(HitWindowIdentity hitWindow)
        {
            if (!_isHitWindowOpen ||
                hitWindow.ComboStep != _activeHitWindowComboStep ||
                !string.Equals(
                    hitWindow.StateName,
                    _activeHitWindowStateName,
                    StringComparison.Ordinal))
            {
                return;
            }

            resetHitFrame();
        }

        private void closeHitFrameForState(string stateName)
        {
            if (!_isHitWindowOpen ||
                !string.Equals(
                    stateName,
                    _activeHitWindowStateName,
                    StringComparison.Ordinal))
            {
                return;
            }

            resetHitFrame();
        }

        /// <summary>
        /// Starts the clock that ends an attack the animation forgot to.
        /// </summary>
        /// <remarks>
        /// Every exit from an attack runs through one SlashEnd event on the
        /// clip, and an animation event is not a guarantee. A clip blended out
        /// early, an interrupted transition, or an event sitting on the very
        /// last frame can all swallow it - and when it is swallowed the attack
        /// never clears. That is not a cosmetic failure: HandleAction refuses
        /// a new attack while one is running and not comboable, and the only
        /// transition out is the autonomic one waiting on the same flag, so
        /// the player is left in a state with no way in and no way out.
        ///
        /// The event stays the way attacks normally end. This is only the
        /// backstop, and it is deliberately slack - longer than the clip it
        /// watches - because a watchdog that could fire during a legitimate
        /// swing would be a worse bug than the one it covers.
        /// </remarks>
        private void armAttackWatchdog(int comboStep)
        {
            if (_comboClipByStep == null)
                buildComboAttackLookup();

            AnimationClip clip = null;
            _comboClipByStep?.TryGetValue(comboStep, out clip);

            _attackWatchdog = clip != null
                ? clip.length * AttackOverrunFactor + AttackOverrunGrace
                : AttackOverrunFallback;
        }

        private void disarmAttackWatchdog()
        {
            _attackWatchdog = 0f;
        }

        /// <summary>
        /// Ends an attack whose SlashEnd never arrived, so the machine can
        /// leave the state it would otherwise be held in.
        /// </summary>
        private void tickAttackWatchdog(float deltaTime)
        {
            if (_attackWatchdog <= 0f)
                return;

            // Something already ended the attack properly; nothing to cover.
            if (!_context.IsAttacking)
            {
                disarmAttackWatchdog();
                return;
            }

            _attackWatchdog -= deltaTime;

            if (_attackWatchdog > 0f)
                return;

            disarmAttackWatchdog();

            // Loud on purpose. This only fires when a clip failed to deliver
            // its own end event, which is an authoring fault worth fixing at
            // the clip rather than leaning on the backstop to hide.
            Debug.LogWarning(
                $"Attack in '{_stateMachine.CurrentStateName}' outlived its " +
                "animation without a SlashEnd event. Ending it so the machine " +
                "can leave the state - check the clip's last event.");

            setAttackSequence(false);
            setCanCombo(false);
            resetHitFrame();
            submitSnapshot();
        }

        private void resetHitFrame()
        {
            bool wasOpen = _isHitWindowOpen;
            string stateName = _activeHitWindowStateName;

            _isHitWindowOpen = false;
            _activeAttack = null;
            _activeHitWindowStateName = null;
            _activeHitWindowComboStep = 0;
            _hitTargets.Clear();

            // Every way a window ends comes through here - its close event,
            // leaving the state, SlashEnd, the watchdog, teardown - so the
            // visuals hear about each of them, not just the clip's own close.
            if (wasOpen && _view != null)
                _view.OnHitWindowChanged(false, stateName);
        }

        private bool isCurrentState(string stateName)
        {
            return string.IsNullOrEmpty(stateName) ||
                string.Equals(
                    stateName,
                    _stateMachine.CurrentStateName,
                    StringComparison.Ordinal);
        }

        private bool tryResolveHitWindowIdentity(
            CombatAnimationFrame frame,
            out HitWindowIdentity hitWindow)
        {
            hitWindow = default;

            if (_comboAttackLookup == null)
                buildComboAttackLookup();

            int comboStep = frame.ComboStep;
            if (comboStep <= 0 &&
                (frame.SourceClip == null ||
                 !_comboStepByClip.TryGetValue(
                     frame.SourceClip,
                     out comboStep)))
            {
                return false;
            }

            if (_comboStateNameLookup == null ||
                !_comboStateNameLookup.TryGetValue(
                    comboStep,
                    out string stateName))
            {
                return false;
            }

            if (!string.IsNullOrEmpty(frame.StateName) &&
                !string.Equals(
                    frame.StateName,
                    stateName,
                    StringComparison.Ordinal))
            {
                return false;
            }

            hitWindow = new HitWindowIdentity(
                stateName,
                comboStep
            );
            return true;
        }

        private void setContextState(CombatType combatType)
        {
            _context.State = combatType;
            _currentCombatType = _context.State;
        }

        private void submitSnapshot()
        {
            _snapshotStreamer.OnNext(Unit.Default);
        }

        private void submitTransitionStream()
        {
            _transitionStreamer.OnNext(_context.State);
        }

        private void setAttackSequence(
            bool isAttacking,
            int comboStep = 0,
            AttackId attack = AttackId.None)
        {
            _context.IsAttacking = isAttacking;
            _context.ComboStep = comboStep;
            _context.Attack = attack;
        }

        private void setCanCombo(bool canCombo)
        {
            _context.CanCombo = canCombo;
        }

        private void setCancelable(bool isCancelable)
        {
            _context.IsCancelable = isCancelable;
        }
        private void resetRequestedStab()
        {
            _context.RequestedStab = AttackId.None;
        }

        private Vector3 findPointToStab()
        {

            return Vector3.zero;
        }

        private void buildComboAttackLookup()
        {
            _comboAttackLookup = new Dictionary<int, string>();
            _comboStepByClip = new Dictionary<AnimationClip, int>();
            _comboClipByStep = new Dictionary<int, AnimationClip>();

            if (_comboAttackKeys == null)
                return;

            foreach (var entry in _comboAttackKeys)
            {
                if (entry.ComboStep <= 0 || string.IsNullOrWhiteSpace(entry.AttackKey))
                    continue;

                _comboAttackLookup[entry.ComboStep] = entry.AttackKey;

                if (entry.HitWindowClip != null)
                {
                    _comboStepByClip[entry.HitWindowClip] =
                        entry.ComboStep;

                    // The same pairing the other way round, so the watchdog
                    // can measure an attack against the clip it is playing.
                    _comboClipByStep[entry.ComboStep] = entry.HitWindowClip;
                }
            }
        }

        /// <summary>
        /// Locomotion ownership for the combat state currently running, read
        /// straight off the attack definition. Resolved per snapshot rather
        /// than cached, because the combo step is the only thing that decides
        /// it and the context already tracks that.
        /// </summary>
        private LocomotionSource resolveLocomotion()
        {
            if (!_context.IsAttacking)
                return LocomotionSource.Simulated;

            return tryResolveAttackDefinition(
                _context.ComboStep,
                out AttackDefinition attack) && attack != null
                    ? attack.Locomotion
                    : LocomotionSource.RootMotion;
        }

        private bool tryResolveAttackDefinition(
            int comboStep,
            out AttackDefinition attack)
        {
            attack = null;

            if (_attackDatabase == null)
            {
                return false;
            }

            if (_comboAttackLookup == null)
                buildComboAttackLookup();

            if (!_comboAttackLookup.TryGetValue(comboStep, out var key))
            {
                return false;
            }

            if (!_attackDatabase.TryGet(key, out attack))
            {
                return false;
            }

            return true;
        }

        private void processHitFrame()
        {
            _view.ProcessHitWindow(_activeAttack, _hitTargets);
        }

        [System.Serializable]
        public class Context
        {
            public CombatType State;
            public bool IsCancelable;
            public bool IsAttacking;
            public bool CanCombo;
            public int ComboStep;
            /// <summary>Which attack the animator should be playing now.</summary>
            public AttackId Attack;

            /// <summary>
            /// The stab variant the last accepted stab action asked for, held
            /// until the stab state opens and can claim it.
            /// </summary>
            public AttackId RequestedStab;
        }


        private readonly struct HitWindowIdentity
        {
            public string StateName { get; }
            public int ComboStep { get; }

            public HitWindowIdentity(
                string stateName,
                int comboStep)
            {
                StateName = stateName;
                ComboStep = comboStep;
            }
        }

        [Serializable]
        private struct ComboAttackKey
        {
            public int ComboStep;
            public string AttackKey;
            public AnimationClip HitWindowClip;
        }
    }
}