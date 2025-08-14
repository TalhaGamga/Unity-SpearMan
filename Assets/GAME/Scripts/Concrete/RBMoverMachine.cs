using DevVorpian;
using R3;
using UnityEngine;

namespace Movement
{
    namespace Mover
    {
        public class RBMoverMachine : IMover
        {
            public MovementType CurrentType => _currentType;

            private MovementType _currentType;
            private Context _context;

            private StateMachine _stateMachine;

            public void Init(IMovementManager movementManager, BehaviorSubject<MovementSnapshot> SnapshotStream, Subject<MovementTransition> TransitionStream)
            {

            }

            public void End()
            {
            }

            public void HandleAction(MovementAction action)
            {
            }

            public void HandleRootMotion(Vector3 delta)
            {
            }

            public void UpdateMover(float deltaTime)
            {
            }

            public class Context
            {
                public Vector2 moveInput;
            }
        }
    }
}
