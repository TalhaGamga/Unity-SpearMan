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
                .Select(_ => new CombatSnapshot(_context.State, _context.IsCancelable, _context.ComboStep, _context.IsAttacking))
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

            _stateMachine.OnTransitionedAutonomously.AddListener(submitAutonomicStateTransition);

            IState idleState = new ConcreteState();
            IState grPrimaryAttackCS1 = new ConcreteState();
            IState grPrimaryAttackCS2 = new ConcreteState();
            IState grPrimaryAttackCS3 = new ConcreteState();

            idleState.OnEnter.AddListener(() =>
            {
                setContextState(CombatType.Idle);
                setAttackSequence(false, 0);
                submitSnapshot();
            });

            grPrimaryAttackCS1.OnEnter.AddListener(() =>
            {
                setContextState(CombatType.GroundedPrimaryAttack);
                setAttackSequence(true, 1);
                submitSnapshot();
            });

            grPrimaryAttackCS2.OnEnter.AddListener(() =>
            {
                setContextState(CombatType.GroundedPrimaryAttack);
                setAttackSequence(true, 2);
                submitSnapshot();
            });

            grPrimaryAttackCS3.OnEnter.AddListener(() =>
            {
                setContextState(CombatType.GroundedPrimaryAttack);
                setAttackSequence(true, 3);
                submitSnapshot();
            });

            var initialIdle = new StateTransition<CombatType>(CombatType.None, CombatType.Idle, idleState, () => Debug.Log("Transitioning to Idle"));
            var idleToGrPrimaryAttackCS1 = new StateTransition<CombatType>(CombatType.Idle, CombatType.GroundedPrimaryAttack, grPrimaryAttackCS1, () => Debug.Log("Transitioning GrPrimaryAttack from Idle"));
            var toGrPrimaryAttackCS2 = new StateTransition<CombatType>(CombatType.GroundedPrimaryAttack, CombatType.GroundedPrimaryAttack, grPrimaryAttackCS2, () => Debug.Log("Transitioning GrPrimaryAttackCS2 from GrPCS1"));
            var toGrPrimaryAttackCS3 = new StateTransition<CombatType>(CombatType.GroundedPrimaryAttack, CombatType.GroundedPrimaryAttack, grPrimaryAttackCS3, () => Debug.Log("Transitioning GrPrimaryAttackCS3 from GrPC2"));

            var attackToIdle = new StateTransition<CombatType>(CombatType.None, CombatType.Idle, idleState, () => !_context.IsAttacking, () => Debug.Log("Transitioning to Idle On Attack End"));

            _stateMachine.AddIntentBasedTransition(initialIdle);
            _stateMachine.AddIntentBasedTransition(idleToGrPrimaryAttackCS1);
            _stateMachine.AddIntentBasedTransition(toGrPrimaryAttackCS2);
            _stateMachine.AddIntentBasedTransition(toGrPrimaryAttackCS3);

            _stateMachine.AddAutonomicTransition(attackToIdle);

            _stateMachine.SetState(CombatType.Idle);
        }

        public void HandleAction(CombatAction action)
        {
            _stateMachine.SetState(action.ActionType);
        }

        public void OnAnimationFrame(CombatAnimationFrame frame)
        {
            if (string.Equals(frame.EventKey, "SlashEnd"))
            {
                _context.IsAttacking = false;
                Debug.Log("Slash End");
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

        private void submitAutonomicStateTransition()
        {
            _transitionStreamer.OnNext(_context.State);
        }

        private void setAttackSequence(bool isAttacking, int comboStep)
        {
            _context.IsAttacking = isAttacking;
            _context.ComboStep = comboStep;
        }

        [System.Serializable]
        public class Context
        {
            public CombatType State;
            public bool IsCancelable;
            public bool IsAttacking;
            public int ComboStep;
        }
    }
}