using Movement.Mover;

namespace Movement
{
    namespace State
    {
        public class RbFall : IState
        {
            public string State => "Fall";

            private RBMoverMachine.Context _context;

            public RbFall(RBMoverMachine.Context context)
            {
                _context = context;
            }

            public void Enter()
            {
                _context.State = MovementType.Fall;

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