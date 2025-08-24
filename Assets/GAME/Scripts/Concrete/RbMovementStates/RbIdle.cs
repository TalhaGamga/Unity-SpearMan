using Movement.Mover;
using UnityEngine;

namespace Movement
{
    namespace State
    {
        public class RbIdle : IState
        {
            public string State => "Idle";

            private RBMoverMachine.Context _context;

            public RbIdle(RBMoverMachine.Context context)
            {
                _context = context;
            }

            public void Enter()
            {
                _context.State = MovementType.Idle;
                _context.Speed = 0;
                _context.Rb.linearVelocity = Vector3.zero;
                _context.SubmitChange();
            }

            public void Exit()
            {
            }

            public void Update()
            {
            }
        }
    }
}