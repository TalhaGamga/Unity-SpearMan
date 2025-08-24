using DevVorpian;
using Movement.State;
using R3;
using System;
using System.Collections.Generic;
using UnityEditor.Rendering;
using UnityEngine;
using UnityEngine.Events;

namespace Movement
{
    namespace Mover
    {
        [System.Serializable]
        public class RBMoverMachine : IMover
        {
            public MovementType CurrentType => MovementType.None;

            [SerializeField] private Context _context;
            [SerializeField] private StateMachine<MovementType> _stateMachine;

            private CompositeDisposable _disposables = new();
            private IMovementManager _manager;

            private Transform[] _groundCheckPoints;
            private float _groundCheckDistance;
            private LayerMask _groundLayer;
            private Transform _characterorientator;
            private Subject<Unit> _submitSnapshotStream = new();
            private BehaviorSubject<MovementType> _submitTransitionStream = new(MovementType.Idle);

            private const float MinSqrDelta = 1e-8f;
            public void Init(IMovementManager movementManager, Subject<MovementSnapshot> snapshotStream, Subject<MovementTransition> transitionStream)
            {
                _stateMachine = new StateMachine<MovementType>();
                _manager = movementManager;
                _groundCheckPoints = _manager.GroundCheckPoints;
                _groundCheckDistance = _manager.GroundCheckDistance;
                _groundLayer = _manager.GroundLayer;
                _characterorientator = _manager.CharacterOrientator;

                _stateMachine.OnTransitionedAutonomously.AddListener(submitAutonomicStateTransition);

                _submitSnapshotStream.
                    Select(_ => new MovementSnapshot(_context.State, _context.Speed, _context.JumpStage))
                    .DistinctUntilChanged()
                    .Subscribe(snapshotStream.OnNext)
                    .AddTo(_disposables);

                _submitTransitionStream
                    .Pairwise()
                    .Subscribe(pair =>
                    {
                        transitionStream.OnNext(new MovementTransition(pair.Previous, pair.Current));
                    }
                    ).AddTo(_disposables);

                IState moveState = new ConcreteState();
                IState idleState = new ConcreteState();
                IState fallState = new ConcreteState();
                IState neutralState = new ConcreteState();

                moveState.OnEnter.AddListener(() =>
                {
                    setContextState(MovementType.Move);
                    submitSnapshot();
                });

                idleState.OnEnter.AddListener(() =>
                {
                    setContextState(MovementType.Idle);
                    submitSnapshot();
                });

                fallState.OnEnter.AddListener(() =>
                {
                    setContextState(MovementType.Fall);
                    submitSnapshot();
                });

                neutralState.OnEnter.AddListener(() =>
                {
                    setContextState(MovementType.Neutral);
                    submitSnapshot();
                });


                moveState.OnUpdate.AddListener(() =>
                {
                    blendSpeed();
                    applyRootMotionAsVelocity();
                    submitSnapshot();
                });

                idleState.OnUpdate.AddListener(() =>
                {
                    blendSpeed();
                    applyRootMotionAsVelocity();
                    submitSnapshot();
                });

                var toMove = new StateTransition<MovementType>(MovementType.None, MovementType.Move, moveState, () => Debug.Log("Transitioning to Move"));
                var toIdle = new StateTransition<MovementType>(MovementType.None, MovementType.Idle, idleState, () => Debug.Log("Transitioning to Idle"));
                var toFall = new StateTransition<MovementType>(MovementType.None, MovementType.Fall, () => !isGrounded(), fallState, () => Debug.Log("Transitioning to Fall"));
                var fallToNeutral = new StateTransition<MovementType>(MovementType.Fall, MovementType.Neutral, () => isGrounded(), neutralState, () => Debug.Log("Transitioning to Neutral"));

                _stateMachine.AddIntentBasedTransition(toMove);
                _stateMachine.AddIntentBasedTransition(toIdle);

                _stateMachine.AddAutonomicTransition(fallToNeutral);
                _stateMachine.AddAutonomicTransition(toFall);

                _stateMachine.SetState(MovementType.Idle);
            }

            public void End()
            {
                _disposables?.Dispose();
                _disposables = null;
            }

            public void HandleAction(MovementAction action)
            {
                _context.MoveInput = action.Direction;
                _stateMachine.SetState(action.ActionType);

                if (Mathf.Approximately(action.Direction.magnitude, 1))
                {
                    var original = _characterorientator.localScale;
                    _characterorientator.localScale = new Vector3(original.x, original.y, action.Direction.x);
                }
            }

            public void HandleRootMotion(RootMotionFrame rootMotion)
            {
                _context.RootMotionDeltaPosition = rootMotion.DeltaPosition;
            }

            public void UpdateMover(float deltaTime)
            {
                _stateMachine.Update();
            }

            private bool isGrounded()
            {
                foreach (var checkPoint in _groundCheckPoints)
                {
                    return Physics.OverlapSphere(checkPoint.position, _groundCheckDistance, _groundLayer).Length > 0;
                }

                return false;
            }

            private void setContextState(MovementType movementType)
            {
                _context.State = movementType;
            }

            private void submitSnapshot()
            {
                _submitSnapshotStream.OnNext(Unit.Default);
            }

            private void submitAutonomicStateTransition()
            {
                _submitTransitionStream.OnNext(_context.State);
            }

            private void blendSpeed()
            {
                float desiredSpeed = _context.MoveInput.magnitude;
                _context.Speed = Mathf.MoveTowards(_context.Speed, desiredSpeed, _context.Acceleration * Time.deltaTime);
            }

            private void applyRootMotionAsVelocity()
            {
                Vector3 delta = _context.RootMotionDeltaPosition;

                Vector2 horizontalVelocity = new Vector2(delta.x, delta.z) / Time.deltaTime;

                Vector3 current = _context.Rb.linearVelocity;
                _context.Rb.linearVelocity = new Vector3(horizontalVelocity.x, current.y, horizontalVelocity.y);

                _context.RootMotionDeltaPosition = Vector3.zero;
            }

            [System.Serializable]
            public class Context
            {
                public Vector2 MoveInput;
                public Vector3 RootMotionDeltaPosition;
                public Rigidbody Rb;
                public float Acceleration;
                public MovementType State;
                public float Speed;
            }
        }
    }
}