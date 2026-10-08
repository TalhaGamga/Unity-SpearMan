using System.Collections.Generic;
using Combat;
using R3;
using Movement;
using UnityEngine;

public class Sword : MonoBehaviour, IWeapon, IWeaponVisualSource, IPierceMotionSource
{
    private const string DashingAttackStateName = "DashingAttack";
    private const string LaunchAttackKey = "Sword_Light_2";
    private const int LaunchArcSegments = 16;

    [SerializeField] private WeaponHitboxSensor _hitbox;
    [SerializeField] private SwordCombatMachine _swordCombatMachine;
    [SerializeField] private WeaponVisualizer _visualizer;
    [SerializeField] private AttackDatabase _attackDatabase;
    [SerializeField] private ReactiveEventDispatcher _dispatcher;

    [Header("Visual Anchors")]
    [Tooltip("Far end of the blade. Falls back to the hitbox centre when unset.")]
    [SerializeField] private Transform _bladeTip;

    [Tooltip("Grip point. Falls back to this weapon's own transform when unset.")]
    [SerializeField] private Transform _handAnchor;

    [Tooltip("The visible weapon. Effects read their reach off this model's " +
        "own extent, so they land on the blade the player can see rather than " +
        "on the damage volume, which is sized for hit detection and sits " +
        "wherever that needs it. Found automatically when left empty.")]
    [SerializeField] private MeshFilter _visual;

    [Header("Hit Feedback")]
    [Tooltip("Impact bursts one swing may spawn. A cut through a crowd - or " +
        "through the fracture pieces a destructive hit has just made - " +
        "otherwise carpets the screen and buries the target it was meant " +
        "to mark.")]
    [SerializeField, Min(1)] private int _maxImpactsPerSwing = 2;

    [Tooltip("Shake the camera on the first confirmed hit of each swing. The " +
        "shake used to be fired by the attack clips themselves, on hits and " +
        "whiffs alike, which made a miss feel exactly like a hit.")]
    [SerializeField] private bool _shakeOnHit = true;

    /// <summary>
    /// Turn rate, radians per second, below which the blade's own rotation
    /// says nothing reliable about which way the edge is moving - a blade
    /// held still, or a sensor that is not sampling at all.
    /// </summary>
    private const float MinTurnRate = 0.5f;

    /// <summary>
    /// Impacts shown since the current hit window confirmed its first target.
    /// </summary>
    private int _impactsThisWindow;

    private Transform _owner;
    private Transform _forwardSource;
    private Vector3 _lastPlanarForward = Vector3.forward;
    private bool _pierceWindowActive;

    // Null-guarded: a weapon whose visual strategy was never authored should
    // simply show nothing, not fail to equip.
    public Observable<VFXPlaySignal> VisualPlayStream => _visualizer?.PlayStream;

    public ICombat CreateCombat(ICombatManager combatManager)
    {
        _owner = (combatManager as Component)?.transform;
        MovementManager movementManager = _owner != null
            ? _owner.GetComponent<MovementManager>()
            : null;
        _forwardSource = movementManager != null
            ? movementManager.CharacterOrientator
            : _owner;
        updatePlanarForward();

        // The weapon is the composition root for everything weapon-specific:
        // it builds its own combat strategy and its own visual strategy, and
        // hands each the rig data it needs.
        _visualizer?.Init(this);

        var logic = _swordCombatMachine;
        logic.SetSwordView(this);
        return logic;
    }

    /// <summary>
    /// Combat and visuals read the same animation signal for different reasons.
    /// This is the visual half; the combat half arrives through ICombat.
    /// </summary>
    public void OnAnimationFrame(CombatAnimationFrame frame)
    {
        _visualizer?.HandleAnimationFrame(frame);
    }

    /// <summary>
    /// Combat opened or closed its hit scan. Reported by the combat machine
    /// itself, after its own checks, so the visuals mark exactly the window
    /// that can hurt something - not every HitFrameOpen a crossfading clip
    /// still fires.
    /// </summary>
    public void OnHitWindowChanged(bool open, string stateName)
    {
        _pierceWindowActive = open &&
            string.Equals(stateName, DashingAttackStateName,
                System.StringComparison.Ordinal);
        _visualizer?.HandleHitWindow(open, stateName);
    }

    private void OnDestroy()
    {
        _visualizer?.End();
    }

    #region IWeaponVisualSource

    public bool IsPierceActive => _pierceWindowActive;

    public bool TryGetPiercePoint(out Vector3 point)
    {
        return TryGetAnchorPosition(VisualAnchor.BladeTip, out point);
    }

    public Vector3 BladeDirection => _hitbox != null
        ? _hitbox.BladeDirection
        : getPlanarForward();

    public Vector3 SwingVelocity => _hitbox != null
        ? _hitbox.Velocity
        : Vector3.zero;

    public float SwingAngularVelocity => _hitbox != null
        ? _hitbox.AngularVelocity
        : 0f;

    public Vector3 PlanarForward => getPlanarForward();

    public bool TryGetAnchor(VisualAnchor anchor, out Transform anchorTransform)
    {
        anchorTransform = anchor switch
        {
            VisualAnchor.BladeTip => _bladeTip != null ? _bladeTip : hitboxTransform(),
            VisualAnchor.Hand => _handAnchor != null ? _handAnchor : transform,
            VisualAnchor.Root => _owner != null ? _owner : transform.root,
            _ => hitboxTransform()
        };

        return anchorTransform != null;
    }

    public bool TryGetAnchorPosition(VisualAnchor anchor, out Vector3 position)
    {
        bool wantsTip = anchor == VisualAnchor.BladeTip && _bladeTip == null;
        bool wantsGrip = anchor == VisualAnchor.Hand && _handAnchor == null;

        // Both ends off the visible model, so effects are drawn along the line
        // the player can actually see. The hitbox is sized and placed for hit
        // detection and its ends sit wherever that needs them.
        if ((wantsTip || wantsGrip) &&
            tryGetVisualBlade(out Vector3 visualGrip, out Vector3 visualTip))
        {
            position = wantsTip ? visualTip : visualGrip;
            return true;
        }

        if (wantsTip && _hitbox != null)
        {
            position = _hitbox.TipPosition;
            return true;
        }

        if (TryGetAnchor(anchor, out Transform anchorTransform))
        {
            position = anchorTransform.position;
            return true;
        }

        position = Vector3.zero;
        return false;
    }

    /// <summary>
    /// The visible model's two ends, in world space.
    ///
    /// Read from the mesh's own bounds along its longest axis, which for any
    /// weapon is the one it is swung with. The end taken as the tip is the one
    /// further from the grip, so the same code works whether the model was
    /// authored pointing forward or back.
    /// </summary>
    private bool tryGetVisualBlade(out Vector3 grip, out Vector3 tip)
    {
        grip = Vector3.zero;
        tip = Vector3.zero;

        if (_visual == null)
            _visual = GetComponentInChildren<MeshFilter>();

        Mesh mesh = _visual != null ? _visual.sharedMesh : null;
        if (mesh == null)
            return false;

        Bounds bounds = mesh.bounds;
        Vector3 extents = bounds.extents;

        Vector3 axis;
        float half;

        if (extents.z >= extents.x && extents.z >= extents.y)
        {
            axis = Vector3.forward;
            half = extents.z;
        }
        else if (extents.y >= extents.x)
        {
            axis = Vector3.up;
            half = extents.y;
        }
        else
        {
            axis = Vector3.right;
            half = extents.x;
        }

        Transform model = _visual.transform;
        Vector3 low = model.TransformPoint(bounds.center - axis * half);
        Vector3 high = model.TransformPoint(bounds.center + axis * half);

        // Measured from where the weapon is held rather than from the
        // character: a weapon held overhead can put its tip nearer the
        // character's origin than its pommel, and the grip never can.
        Vector3 held = _handAnchor != null ? _handAnchor.position : transform.position;
        bool lowIsFar = (low - held).sqrMagnitude >= (high - held).sqrMagnitude;

        tip = lowIsFar ? low : high;
        grip = lowIsFar ? high : low;
        return true;
    }

    private Transform hitboxTransform()
    {
        return _hitbox != null ? _hitbox.Center : transform;
    }

    #endregion

    public bool TryGetAttackDefinition(string key, out AttackDefinition attack)
    {
        attack = null;

        if (_attackDatabase == null)
            return false;

        return _attackDatabase.TryGet(key, out attack);
    }

    public void ProcessHitWindow(AttackDefinition attack, HashSet<GameObject> hitTargets)
    {
        if (attack == null || _hitbox == null || _dispatcher == null)
            return;

        // Dash-stab moves the character in FixedUpdate but combat samples in
        // Update. Let its hitbox sweep the full traversal distance so a low
        // render frame cannot place the blade on opposite sides of a target
        // without reporting the crossing.
        var hits = _hitbox.ScanHits(attack.HasPierce);

        foreach (PlanarWeaponHit planarHit in hits)
        {
            Collider hit = planarHit.Collider;

            if (IsOwnerCollider(hit))
                continue;

            GameObject target = hit.attachedRigidbody != null
                ? hit.attachedRigidbody.gameObject
                : hit.gameObject;

            if (target == null)
                continue;

            if (hitTargets.Contains(target))
                continue;

            hitTargets.Add(target);

            Vector3 velocity = _hitbox.Velocity;
            Vector3 hitPoint = planarHit.Point;

            // Both ends of the fallback flattened before they are subtracted.
            // Taking the difference first and flattening after would give the
            // same answer here, but only because both happen to be points -
            // the rule is that a gameplay vector is built out of gameplay
            // values, so that it stays true when somebody changes one end of
            // it later.
            Vector3 targetCenter = GameplayPlane.Flatten(
                hit.attachedRigidbody != null
                    ? hit.attachedRigidbody.worldCenterOfMass
                    : hit.bounds.center);
            Vector3 attackOrigin = GameplayPlane.Flatten(
                _owner != null
                    ? _owner.position
                    : _hitbox.Position);
            Vector3 fallbackDirection = targetCenter - attackOrigin;
            var hitContext = new HitContext(
                velocity,
                hitPoint,
                fallbackDirection,
                _hitbox.BladeDirection,
                getPlanarForward()
            );

            var source = new AttackReactiveEventSource(
                attack,
                hitContext,
                this
            );

            // Presentation before rules. A destructive or slicing attack may
            // replace this collider over the next few frames, and the impact
            // needs it whole to find the side of the body facing the camera.
            // This is also the only place a hit and a miss can be told apart:
            // a window that closes without ever reaching this line was a
            // miss, and nothing below fires for it.
            bool isFirstOfSwing = hitTargets.Count == 1;

            // The hit set is emptied every time a window opens or closes, so
            // its first entry is also the first moment of a new window.
            if (isFirstOfSwing)
                _impactsThisWindow = 0;

            if (shouldShowImpact(target))
            {
                // The shake rides the first impact rather than the first
                // entry in the hit set, so it says what the burst says: that
                // something which can be hurt was hurt. A cut through leftover
                // fracture chips is a confirmed overlap, not a hit anyone
                // should feel.
                if (_impactsThisWindow == 0 && _shakeOnHit)
                    CameraManager.Shake();

                _impactsThisWindow++;
                _visualizer?.HandleHit(new WeaponHit(
                    attack,
                    target,
                    hit,
                    hitPoint,
                    targetCenter,
                    resolveSlashDirection(hitContext),
                    hitContext.Speed,
                    isFirstOfSwing));
            }

            _dispatcher.Apply(source, target);
        }
    }

    /// <summary>
    /// Whether this hit earns an impact burst.
    ///
    /// Capabilities are looked up on the target object itself, the same rule
    /// TargetContext applies when the rules run, so what sparks is exactly
    /// what the attack can hurt or stagger: characters (IHitReactable) and
    /// test boxes (IDamageable) do, scenery and loose fracture pieces - which
    /// carry nothing but a rigidbody response - do not. The cap counts the
    /// impacts this window has actually shown, not its place in the hit set:
    /// fracture pieces left lying in the arc by an earlier finisher join the
    /// set too, and counting them would let two loose chips use up the
    /// swing's impacts before the blade ever reached the enemy behind them.
    /// </summary>
    private bool shouldShowImpact(GameObject target)
    {
        if (target == null || _impactsThisWindow >= _maxImpactsPerSwing)
            return false;

        return target.GetComponent<IHitReactable>() != null ||
               target.GetComponent<IDamageable>() != null;
    }

    /// <summary>
    /// Which way the edge travelled through the target, on the plane.
    ///
    /// Read off the blade's turn first. A positive turn rate rotates the blade
    /// from +Z toward +Y, so the edge moves along the blade rotated a quarter
    /// turn the same way in (Z, Y): (0, b.z, -b.y) for blade heading b. The
    /// linear velocity comes second because a lunge carries the whole
    /// character, and that travel swamps the sideways sweep a cut shows up in.
    /// The attacker-to-target direction HitContext falls back to comes after
    /// that, and facing last, so a sensor that is not sampling still gives an
    /// impact that points somewhere sensible.
    /// </summary>
    private Vector3 resolveSlashDirection(in HitContext hitContext)
    {
        if (_hitbox != null && Mathf.Abs(_hitbox.AngularVelocity) > MinTurnRate)
        {
            Vector3 blade = _hitbox.BladeDirection;
            Vector3 edge = new Vector3(0f, blade.z, -blade.y) *
                Mathf.Sign(_hitbox.AngularVelocity);

            if (edge.sqrMagnitude > 1e-6f)
                return edge.normalized;
        }

        Vector3 direction = hitContext.Direction;
        return direction.sqrMagnitude > 1e-6f
            ? direction
            : getPlanarForward();
    }

    private Vector3 getPlanarForward()
    {
        updatePlanarForward();
        return _lastPlanarForward;
    }

    private void updatePlanarForward()
    {
        if (_forwardSource == null)
            return;

        float forwardZ = _forwardSource.forward.z;
        if (Mathf.Abs(forwardZ) > 0.1f)
        {
            _lastPlanarForward = Vector3.forward *
                Mathf.Sign(forwardZ);
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (_hitbox == null ||
            _attackDatabase == null ||
            !_attackDatabase.TryGet(
                LaunchAttackKey,
                out AttackDefinition launchAttack) ||
            launchAttack == null)
        {
            return;
        }

        PhysicsResponseSettings motion = launchAttack.Impact.Motion;
        if (!motion.ClampToForwardArc)
            return;

        resolveGizmoForwardSource();

        Vector3 origin = _hitbox.Position;
        Vector3 forward = getPlanarForward();
        float minimumAngle = Mathf.Clamp(
            Mathf.Min(
                motion.MinimumForwardAngle,
                motion.MaximumForwardAngle
            ),
            0f,
            90f
        );
        float maximumAngle = Mathf.Clamp(
            Mathf.Max(
                motion.MinimumForwardAngle,
                motion.MaximumForwardAngle
            ),
            minimumAngle,
            90f
        );
        float rayLength = Mathf.Clamp(
            motion.LinearForce * 0.15f,
            1f,
            4f
        );

        Gizmos.color = Color.blue;
        Gizmos.DrawRay(origin, forward * rayLength * 0.55f);

        Vector3 previousPoint = origin + getLaunchArcDirection(
            forward,
            minimumAngle
        ) * rayLength;

        Gizmos.color = new Color(1f, 0.55f, 0f, 1f);
        Gizmos.DrawLine(origin, previousPoint);

        for (int i = 1; i <= LaunchArcSegments; i++)
        {
            float interpolation = i / (float)LaunchArcSegments;
            float angle = Mathf.Lerp(
                minimumAngle,
                maximumAngle,
                interpolation
            );
            Vector3 point = origin + getLaunchArcDirection(
                forward,
                angle
            ) * rayLength;
            Gizmos.DrawLine(previousPoint, point);
            previousPoint = point;
        }

        Gizmos.DrawLine(origin, previousPoint);

#if UNITY_EDITOR
        UnityEditor.Handles.Label(
            origin + getLaunchArcDirection(
                forward,
                minimumAngle
            ) * rayLength,
            $"Launch min {minimumAngle:0}°"
        );
        UnityEditor.Handles.Label(
            origin + getLaunchArcDirection(
                forward,
                maximumAngle
            ) * rayLength,
            $"Launch max {maximumAngle:0}°"
        );
#endif
    }

    private void resolveGizmoForwardSource()
    {
        if (_forwardSource != null)
            return;

        MovementManager movementManager =
            GetComponentInParent<MovementManager>();
        _owner = movementManager != null
            ? movementManager.transform
            : transform.root;
        _forwardSource = movementManager != null
            ? movementManager.CharacterOrientator
            : _owner;
        updatePlanarForward();
    }

    private static Vector3 getLaunchArcDirection(
        Vector3 forward,
        float angle)
    {
        float radians = angle * Mathf.Deg2Rad;
        return forward * Mathf.Cos(radians) +
            Vector3.up * Mathf.Sin(radians);
    }

    private bool IsOwnerCollider(Collider hit)
    {
        if (_owner == null || hit == null)
            return false;

        if (hit.transform == _owner || hit.transform.IsChildOf(_owner))
            return true;

        Transform rigidbodyTransform = hit.attachedRigidbody?.transform;
        return rigidbodyTransform != null &&
               (rigidbodyTransform == _owner || rigidbodyTransform.IsChildOf(_owner));
    }
}
