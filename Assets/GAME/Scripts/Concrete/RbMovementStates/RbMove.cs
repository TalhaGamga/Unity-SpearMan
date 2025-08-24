using Movement.Mover;
using Unity.Hierarchy;
using UnityEngine;

namespace Movement
{
    namespace State
    {
        public class RbMove : IState
        {
            public string State => "Move";

            private RBMoverMachine.Context _context;
            private Rigidbody _rb;

            private const float MinSqrDelta = 1e-8f;
            private const float MinMaxSpeed = 0.01f;
            public RbMove(RBMoverMachine.Context context)
            {
                _context = context;
                _rb = context.Rb;
            }

            public void Enter()
            {
                _context.State = MovementType.Move;
                _context.SubmitChange();
            }

            public void Exit()
            {   
            }   

            public void Update()
            {
                var deltaTime = Time.deltaTime;
                if (deltaTime <= 0f) return;

                bool changed = false;

                changed |= updateAnimBlend(deltaTime);
                changed |= applyRootMotionAsVelocity(deltaTime);

                if (changed)
                    _context.SubmitChange();
            }
            private bool updateAnimBlend(float dt)
            {
                float target = Mathf.Clamp01(_context.MoveInput.magnitude);
                float step = _context.Acceleration * dt;

                float blended = Mathf.MoveTowards(_context.Speed, target, step);
                if (!Mathf.Approximately(blended, _context.Speed))
                {
                    _context.Speed = blended;
                    return true;
                }

                return false;
            }

            private bool applyRootMotionAsVelocity(float deltaTime)
            {
                Vector3 delta = _context.RootMotionDeltaPosition;

                if (delta.sqrMagnitude < MinSqrDelta)
                    return false;

                Vector2 horizontalVelocity = new Vector2(delta.x, delta.z) / deltaTime;

                Vector3 current = _rb.linearVelocity;
                _rb.linearVelocity = new Vector3(horizontalVelocity.x, current.y, horizontalVelocity.y);

                _context.RootMotionDeltaPosition = Vector3.zero;
                return true;
            }
        }
    }
}