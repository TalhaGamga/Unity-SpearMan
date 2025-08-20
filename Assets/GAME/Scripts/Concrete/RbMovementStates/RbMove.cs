using Movement.Mover;
using UnityEngine;

namespace Movement
{
    namespace State
    {
        public class RbMove : IState
        {
            public string State => "RbMove";

            private RBMoverMachine.Context _context;
            private Rigidbody _rb;
            public RbMove(RBMoverMachine.Context context)
            {
                _context = context;
                _rb = context.Rb;
            }

            public void Enter()
            {
                Debug.Log("Rb Move Entered");
            }

            public void Exit()
            {
            }

            public void Tick()
            {
            }

            public void Update()
            {
                float deltaTime = Time.deltaTime;
                Vector3 rootMotionDelta = _context.RootMotionDelta;
                float zRootSpeed = rootMotionDelta.z / deltaTime;
                float xRootSpeed = rootMotionDelta.x / deltaTime;
                Vector3 rootMotionVelocity = new Vector3(0, 0, _context.Speed * _context.MoveInput.x);
                _rb.linearVelocity = rootMotionVelocity;
            }
        }
    }
}