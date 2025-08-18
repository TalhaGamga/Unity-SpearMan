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

            [SerializeField] private StateMachine<MovementType> _stateMachine;
            public void Init(IMovementManager movementManager, BehaviorSubject<MovementSnapshot> SnapshotStream, Subject<MovementTransition> TransitionStream)
            {
                _stateMachine = new StateMachine<MovementType>();
                RbMove moveState = new RbMove(_context);
                RbIdle idleState = new RbIdle(_context);
                StateTransition<MovementType> toMove = new StateTransition<MovementType>(MovementType.None, MovementType.Move, moveState);
                StateTransition<MovementType> toIdle = new StateTransition<MovementType>(MovementType.None, MovementType.Idle, idleState);
                StateTransition<MovementType> moveToIdle = new StateTransition<MovementType>(MovementType.Move, MovementType.Idle, idleState);

                toMove.SetOnTransition(() => Debug.Log("Transitioning to Move"));
                toIdle.SetOnTransition(() => Debug.Log("Transitioning to Idle"));
                moveToIdle.SetOnTransition(() => Debug.Log("Transitioning to Idle from Move"));

                _stateMachine.AddTransition(toMove);
                _stateMachine.AddTransition(toIdle);
                _stateMachine.AddTransition(moveToIdle);

                _stateMachine.SetState(MovementType.Idle);
            }

            public void End()
            {
            }

            public void HandleAction(MovementAction action)
            {
                _stateMachine.SetState(action.ActionType);
                _context.moveInput = action.Direction;
            }

            public void HandleRootMotion(Vector3 delta)
            {
            }

            public void UpdateMover(float deltaTime)
            {
                _stateMachine.Update();
                _currentType = _stateMachine._currentStateType;
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