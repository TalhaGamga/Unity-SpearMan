using Movement.Mover;
using UnityEngine;

namespace Movement
{
    namespace State
    {
        public class RbNeutral : IState
        {
            public string State => "Neutral";

            private RBMoverMachine.Context _context;

            public RbNeutral(RBMoverMachine.Context context)
            {
                _context = context;
            }

            public void Enter()
            {
                _context.State = MovementType.Neutral;
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