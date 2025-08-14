using Movement.Mover;

namespace Movement
{
    namespace State
    {
        public class RbMove : IState
        {
            public string State => "RbRun";

            private RBMoverMachine.Context _context;

            public RbMove(RBMoverMachine.Context context)
            {
                _context = context;
            }

            public void Enter()
            {
                throw new System.NotImplementedException();
            }

            public void Exit()
            {
                throw new System.NotImplementedException();
            }

            public void Tick()
            {
                throw new System.NotImplementedException();
            }

            public void Update()
            {
                throw new System.NotImplementedException();
            }
        }
    }
}