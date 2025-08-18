using Movement.Mover;

namespace Movement
{
    namespace State
    {
        public class RbIdle : IState
        {
            public string State => "RbIdle";

            private RBMoverMachine.Context _context;

            public RbIdle(RBMoverMachine.Context context)
            {
                _context = context;
            }

            public void Enter()
            {
            }

            public void Exit()
            {
            }

            public void Tick()
            {
            }

            public void Update()
            {
            }
        }
    }
}