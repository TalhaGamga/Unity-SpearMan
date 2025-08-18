using DevVorpian;
using Movement.State;
using R3;
using UnityEngine;

namespace Movement
{
    namespace Mover
    {
        [System.Serializable]
        public class RBMoverMachine : IMover
        {
            public MovementType CurrentType => _currentType;

            [HideInInspector] private MovementType _currentType;
            [SerializeField] private Context _context;

            [SerializeField] private StateMachine _stateMachine;
            RbIdle idle;
            RbMove move;
            public void Init(IMovementManager movementManager, BehaviorSubject<MovementSnapshot> SnapshotStream, Subject<MovementTransition> TransitionStream)
            {
                _stateMachine = new StateMachine();
                idle = new RbIdle(_context);
                move = new RbMove(_context);
                StateTransition toIdle = new StateTransition(idle);
                toIdle.SetOnTransition(() => Debug.Log("Transitioning to Idle"));

                StateTransition toMove = new StateTransition(move);
                toMove.SetOnTransition(() => Debug.Log("Transitioning to Move"));

                _stateMachine.AddAnyTransition(toIdle);
                _stateMachine.AddAnyTransition(toMove);

                _stateMachine.SetState(idle);
            }

            public void End()
            {
            }

            public void HandleAction(MovementAction action)
            {
                if (action.ActionType == MovementType.Move)
                {
                    _stateMachine.SetState(move);
                }

                if (action.ActionType == MovementType.Idle)
                {
                    _stateMachine.SetState(idle);
                }
            }

            public void HandleRootMotion(Vector3 delta)
            {
            }

            public void UpdateMover(float deltaTime)
            {
                _stateMachine.Update();
            }

            [System.Serializable]
            public class Context
            {
                public Vector2 moveInput;
                public float speed;
            }
        }
    }
}
