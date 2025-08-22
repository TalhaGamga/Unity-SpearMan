using Movement.Mover;

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