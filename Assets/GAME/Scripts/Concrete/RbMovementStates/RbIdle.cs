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