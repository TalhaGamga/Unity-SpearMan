using Movement.Mover;

namespace Movement
{
    namespace State
    {
        public class RbMove : IState
        {
            public string State => "RbMove";

            private RBMoverMachine.Context _context;

            public RbMove(RBMoverMachine.Context context)
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