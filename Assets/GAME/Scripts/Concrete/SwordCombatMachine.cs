using DevVorpian;
using Movement.State;
using R3;
using UnityEngine;

namespace Combat
{
    [System.Serializable]
    public class SwordCombatMachine : ICombat
    {
        public CombatType CombatType => _currentCombatType;

        [SerializeField] private Context _context;
        [SerializeField] private StateMachine<CombatType> _stateMachine;

        private readonly Sword _view;
        private readonly Subject<Unit> _snapshotStreamer = new();
        private readonly BehaviorSubject<CombatType> _transitionStreamer = new(CombatType.Idle);

        private CompositeDisposable _disposables = new();
        private CombatSnapshot _currentSnapshot = CombatSnapshot.Default;
        private CombatType _currentCombatType;
        //public SwordCombatMachine(Sword view)
        //{
        //    _view = view;
        //}

        public void Init(ICombatManager combatManager, Subject<CombatSnapshot> snapshotStream, Subject<CombatTransition> transitionStream)
        {
            _stateMachine = new StateMachine<CombatType>();

            _snapshotStreamer
                .Select(_ => new CombatSnapshot(_context.State, _context.Version, _context.IsCancelable, _context.ComboStep, _context.IsAttacking))
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
            var stab = new ConcreteState("Stab");

            #region OnEnter
            idleState.OnEnter.AddListener(() =>
            {
                setContextState(CombatType.Idle);
                setAttackSequence(false, 0);
                setCanCombo(false);
                setCancelable(false);

                submitSnapshot();
            });

            grPA_S1.OnEnter.AddListener(() =>
            {
                setContextState(CombatType.GroundedPrimaryAttack);
                setAttackSequence(true, 1);
                setCanCombo(false);
                setCancelable(false);

                submitSnapshot();
            });

            grPA_S2.OnEnter.AddListener(() =>
            {
                setContextState(CombatType.GroundedPrimaryAttack);
                setAttackSequence(true, 2);
                setCanCombo(false);
                setCancelable(false);

                submitSnapshot();
            });

            grPA_S3.OnEnter.AddListener(() =>
            {
                setContextState(CombatType.GroundedPrimaryAttack);
                setAttackSequence(true, 3);
                setCanCombo(false);
                setCancelable(false);

                submitSnapshot();
            });

            stab.OnEnter.AddListener(() =>
            {
                setContextState(CombatType.Stab);
                setAttackSequence(true, 0);
                setCanCombo(false);
                setCancelable(false);

                submitSnapshot();
            });
            #endregion


            #region OnExit
            grPA_S1.OnExit.AddListener(() =>
            {
                setAttackSequence(false);
                setCanCombo(false);
                setCancelable(false);

                submitSnapshot();
            });

            grPA_S2.OnExit.AddListener(() =>
            {
                setAttackSequence(false);
                setCanCombo(false);
                setCancelable(false);

                submitSnapshot();
            });

            grPA_S3.OnExit.AddListener(() =>
            {
                setAttackSequence(false);
                setCanCombo(false);
                setCancelable(false);

                submitSnapshot();
            });

            stab.OnExit.AddListener(() =>
            {
                setAttackSequence(false);
                setCanCombo(false);
                setCancelable(false);
                resetVersion();

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
            _context.Version = action.Version;
            _stateMachine.SetState(action.ActionType);
        }

        public void OnAnimationFrame(CombatAnimationFrame frame)
        {
            if (string.Equals(frame.StateName, _stateMachine.CurrentStateName))
            {
                return;
            }

            switch (frame.EventKey)
            {
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
                    break;
                default:
                    break;
            }
        }

        public void OnWeaponCollision(Collider other)
        {
        }

        public void Update(float deltaTime)
        {
            _stateMachine.Update();
        }

        public void End()
        {
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

        private void setAttackSequence(bool isAttacking, int comboStep = 0)
        {
            _context.IsAttacking = isAttacking;
            _context.ComboStep = comboStep;
        }

        private void setCanCombo(bool canCombo)
        {
            _context.CanCombo = canCombo;
        }

        private void setCancelable(bool isCancelable)
        {
            _context.IsCancelable = isCancelable;
        }
        private void resetVersion()
        {
            _context.Version = 0;
        }

        private Vector3 findPointToStab()
        {

            return Vector3.zero;
        }

        [System.Serializable]
        public class Context
        {
            public CombatType State;
            public bool IsCancelable;
            public bool IsAttacking;
            public bool CanCombo;
            public int ComboStep;
            public int Version = 0;
        }
    }
}