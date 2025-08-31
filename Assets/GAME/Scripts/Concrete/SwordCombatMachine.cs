using DevVorpian;
using Movement.State;
using R3;
using UnityEngine;

namespace Combat
{
    [System.Serializable]
    public class SwordCombatMachine : ICombat
    {
        public CombatType CombatType => CombatType.None;

        [SerializeField] private Context _context;
        [SerializeField] private StateMachine<CombatType> _stateMachine;

        private readonly Sword _view;
        private readonly Subject<Unit> _snapshotStreamer = new();
        private readonly BehaviorSubject<CombatType> _transitionStreamer = new(CombatType.Idle);

        private CompositeDisposable _disposables = new();
        private CombatSnapshot _currentSnapshot = CombatSnapshot.Default;

        public SwordCombatMachine(Sword view)
        {
            _view = view;
        }

        public void Init(ICombatManager combatManager, Subject<CombatSnapshot> snapshotStream, Subject<CombatTransition> transitionStream)
        {
            _stateMachine = new StateMachine<CombatType>();

            _snapshotStreamer
                .Select(_ => new CombatSnapshot(_context.State, _context.IsCancelable, _context.ComboStep))
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

            IState idleState = new ConcreteState();
            IState grPrimaryAttackCS1 = new ConcreteState();
            IState grPrimaryAttackCS2 = new ConcreteState();
            IState grPrimaryAttackCS3 = new ConcreteState();

            var idleToGrPrimaryAttackCS1 = new StateTransition<CombatType>(CombatType.Idle, CombatType.GroundedPrimaryAttack, grPrimaryAttackCS1, () => Debug.Log("Transitioning grPrimaryAttack from Idle"));
            var toGrPrimaryAttackCS2 = new StateTransition<CombatType>(CombatType.GroundedPrimaryAttack, CombatType.GroundedPrimaryAttack, grPrimaryAttackCS2, () => Debug.Log("Transitioning grPrimaryAttackCS2 from GRPCS1"));
            //var toGrPrimaryAttackCS3 = new StateTransition<CombatType>(CombatType.)
        }

        public void HandleInput(CombatAction action)
        {
        }

        public void OnAnimationFrame(CombatAnimationFrame frame)
        {
        }

        public void OnWeaponCollision(Collider other)
        {
        }

        public void Update(float deltaTime)
        {
        }

        public void End()
        {
        }

        [System.Serializable]
        public class Context
        {
            public CombatType State;
            public bool IsCancelable;
            public int ComboStep;
        }
    }
}