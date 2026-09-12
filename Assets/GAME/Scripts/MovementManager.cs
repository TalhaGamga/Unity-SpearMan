using Movement.Mover;
using R3;
using System.Collections.Generic;
using UnityEngine;

namespace Movement
{
    [RequireComponent(typeof(Rigidbody))]
    public class MovementManager : MonoBehaviour, IMovementManager, IMovementInputReceiver, IReactiveCapabilityProvider
    {
        public Subject<MovementSnapshot> SnapshotStream { get; } = new();
        public Subject<MovementTransition> TransitionStream { get; } = new();

        public Transform CharacterOrientator => _characterModelTransform;
        public Transform CharacterTranslater => _characterTransform;
        public Transform[] GroundCheckPoints => _groundCheckPoints;
        public float GroundCheckDistance => _groundCheckDistance;
        public LayerMask GroundLayer => _groundLayer;
        /// <summary>
        /// Contacts first, probe second.
        ///
        /// Collision contacts alone are not a reliable ground test: crossing the
        /// seam between two butted colliders can report Exit on the old one
        /// before Enter on the new one, and a single dropped frame is enough to
        /// throw the mover into Fall in the middle of a flat run. The probe
        /// covers that gap using the foot transforms the prefab has always
        /// carried for exactly this purpose.
        /// </summary>
        public bool HasGroundContact =>
            _groundContacts.Count > 0 || probeGround();
        public float SpeedModifier => _speedModifier;
        public float JumpModifier => _jumpModifier;
        public Vector3 GroundNormal
        {
            get
            {
                Vector3 combinedNormal = Vector3.zero;

                foreach (var contact in _groundContacts)
                {
                    if (contact.Key != null)
                        combinedNormal += contact.Value;
                }

                return combinedNormal.sqrMagnitude > 1e-6f
                    ? combinedNormal.normalized
                    : Vector3.up;
            }
        }

        [SerializeField] private Transform _characterModelTransform;
        [SerializeField] private Transform _characterTransform;
        [SerializeField] private Transform[] _groundCheckPoints;
        [SerializeField] private LayerMask _groundLayer;
        [SerializeField] private float _groundCheckDistance = 0.1f;
        [SerializeField, Range(0f, 1f)]
        private float _minGroundNormalY = 0.5f;
        [SerializeField] private RBMoverMachine _rbMoverMachine;
        [HideInInspector] private MovementType currentState;

        private IMover _currentMover;
        private float _speedModifier = 1f;
        private float _jumpModifier = 1f;
        private float _currentSpeed;

        private readonly BehaviorSubject<(bool, string)> _movability = new((true, ""));
        private readonly BehaviorSubject<(bool, string)> _jumpability = new((true, ""));
        private readonly Subject<MovementSnapshot> _stream = new();
        private readonly CompositeDisposable _disposables = new();
        private readonly Dictionary<Collider, Vector3> _groundContacts =
            new();
        private readonly ContactPoint[] _contactBuffer =
            new ContactPoint[16];

        private Rigidbody _rigidbody;

        private void OnDrawGizmosSelected()
        {
            if (_groundCheckPoints != null)
            {
                Gizmos.color = Color.green;
                foreach (var checkPoint in _groundCheckPoints)
                {
                    if (checkPoint == null) continue;
                    Gizmos.DrawSphere(checkPoint.position, _groundCheckDistance);
                }
            }
        }

        private void Awake()
        {
            _rigidbody = GetComponent<Rigidbody>();
            SetMover(_rbMoverMachine);
        }

        private void OnDestroy()
        {
            _groundContacts.Clear();
            _disposables.Dispose();
        }

        private void OnDisable()
        {
            _groundContacts.Clear();
        }

        private void OnCollisionEnter(Collision collision)
        {
            updateGroundContact(collision);
        }

        private void OnCollisionStay(Collision collision)
        {
            updateGroundContact(collision);
        }

        private void OnCollisionExit(Collision collision)
        {
            if (collision.collider != null)
                _groundContacts.Remove(collision.collider);
        }

        private void Update()
        {
            _currentMover?.UpdateMover(Time.deltaTime);
            currentState = _currentMover != null
                ? _currentMover.CurrentType
                : MovementType.None;
        }

        private void FixedUpdate()
        {
            _currentMover?.PhysicsUpdateMover(Time.fixedDeltaTime);
        }

        public void SetMover(IMover newMover)
        {
            _currentMover?.End();
            _currentMover = newMover;
            _currentMover?.Init(this, SnapshotStream, TransitionStream);
            _disposables.Clear();

            SnapshotStream.AddTo(_disposables);
        }

        public void SetSpeedModifier(float newModifier) =>
            _speedModifier = Mathf.Max(0f, newModifier);

        public void SetJumpModifier(float newModifier) =>
            _jumpModifier = Mathf.Max(0f, newModifier);

        public void HandleAction(MovementAction action)
        {
            _currentMover.HandleAction(action);
        }

        public void HandleRootMotion(RootMotionFrame rootMotion)
        {
            _currentMover.HandleRootMotion(rootMotion);
        }

        public void HandleImpact(ImpactData impact)
        {
            _currentMover?.HandleImpact(impact);
        }

        public bool GetIsGrounded()
        {
            return HasGroundContact;
        }

        /// <summary>
        /// Casts down from each foot point. Starts slightly above the foot so
        /// the ray is never fired from inside the floor the character is
        /// already pressing into, which would return nothing.
        /// </summary>
        private bool probeGround()
        {
            if (_groundCheckPoints == null)
                return false;

            float lift = _groundCheckDistance;
            float reach = lift + _groundCheckDistance;

            foreach (Transform checkPoint in _groundCheckPoints)
            {
                if (checkPoint == null)
                    continue;

                Vector3 origin = checkPoint.position + Vector3.up * lift;

                if (!Physics.Raycast(
                        origin,
                        Vector3.down,
                        out RaycastHit hit,
                        reach,
                        _groundLayer,
                        QueryTriggerInteraction.Ignore))
                {
                    continue;
                }

                if (hit.normal.y >= _minGroundNormalY)
                    return true;
            }

            return false;
        }

        private void updateGroundContact(Collision collision)
        {
            Collider groundCollider = collision.collider;
            if (groundCollider == null)
                return;

            int groundLayer = collision.gameObject.layer;
            if ((_groundLayer.value & (1 << groundLayer)) == 0)
            {
                _groundContacts.Remove(groundCollider);
                return;
            }

            int contactCount = collision.GetContacts(_contactBuffer);
            Vector3 bestGroundNormal = Vector3.zero;

            for (int i = 0; i < contactCount; i++)
            {
                ContactPoint contact = _contactBuffer[i];
                Vector3 normal = contact.normal;
                Vector3 towardBody = _rigidbody.worldCenterOfMass -
                    contact.point;

                if (Vector3.Dot(normal, towardBody) < 0f)
                    normal = -normal;

                if (normal.y >= _minGroundNormalY &&
                    normal.y > bestGroundNormal.y)
                {
                    bestGroundNormal = normal;
                }
            }

            if (bestGroundNormal.y >= _minGroundNormalY)
                _groundContacts[groundCollider] = bestGroundNormal;
            else
                _groundContacts.Remove(groundCollider);
        }

        public Observable<(bool Allowed, string Reason)> ObserveCapability(Capability capability)
        {
            return capability switch
            {
                Capability.Movability => _movability,
                Capability.Jumpability => _jumpability,
                _ => Observable.Return((true, ""))
            };
        }

        public void OnAnimationFrame(MovementAnimationFrame animationFrame)
        {
            _currentMover.OnAnimationFrame(animationFrame);
        }
    }

}
