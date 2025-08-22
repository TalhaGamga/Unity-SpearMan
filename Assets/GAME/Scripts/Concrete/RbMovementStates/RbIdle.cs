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
                _context.State = MovementType.Idle;
                _context.Speed -= 1;
                _context.JumpStage -= 1;
                _context.SubmitChange();
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