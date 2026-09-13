using R3;
using UnityEngine;

namespace Combat
{
    /// <summary>
    /// Turns a raw animation signal into a concrete effect request.
    ///
    /// This is the only place in the chain that knows both halves: the cue key
    /// the clip fired, and what this particular weapon looks like. Everything
    /// upstream carries a weapon-agnostic signal; everything downstream just
    /// pools whatever it is handed.
    ///
    /// Serialized into the weapon that owns it, matching how
    /// <see cref="SwordCombatMachine"/> and RBMoverMachine are serialized into
    /// their owners - a strategy lives inside the thing whose behaviour it is.
    /// </summary>
    [System.Serializable]
    public class WeaponVisualizer
    {
        private const string VisualCueEventKey = "VisualCue";

        [Tooltip("This weapon's cue dictionary. Shared freely between weapon " +
            "variants that should look the same.")]
        [SerializeField] private WeaponVisualPack _pack;

        [Tooltip("Log once per unknown cue key. Leave on while authoring - a " +
            "typo in a clip event is otherwise completely silent.")]
        [SerializeField] private bool _warnOnUnknownCue = true;

        private readonly Subject<VFXPlaySignal> _play = new();
        private readonly System.Collections.Generic.HashSet<string> _warned = new();

        private IWeaponVisualSource _source;

        public Observable<VFXPlaySignal> PlayStream => _play;

        public void Init(IWeaponVisualSource source)
        {
            _source = source;
            _warned.Clear();
        }

        public void End()
        {
            _source = null;
        }

        /// <summary>
        /// Single entry point. Everything that is not a visual cue falls
        /// straight through - combat listens to the same frames for its own
        /// reasons and the two must not interfere.
        /// </summary>
        public void HandleAnimationFrame(CombatAnimationFrame frame)
        {
            if (_pack == null || _source == null)
                return;

            if (!string.Equals(
                    frame.EventKey,
                    VisualCueEventKey,
                    System.StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            if (!_pack.TryGetCue(frame.Cue, out VisualCueEntry cue))
            {
                warnUnknownCue(frame.Cue);
                return;
            }

            if (cue.Prefab == null)
                return;

            if (!_source.TryGetAnchor(cue.Anchor, out Transform anchor) ||
                anchor == null)
            {
                return;
            }

            _play.OnNext(buildSignal(cue, anchor));
        }

        private VFXPlaySignal buildSignal(VisualCueEntry cue, Transform anchor)
        {
            float strength = resolveSwingStrength(cue);

            Vector3 scale = cue.Scale;
            if (cue.ScaleBy == VisualScalar.SwingSpeed)
                scale *= strength;

            float playbackRate = cue.RateBy == VisualScalar.SwingSpeed
                ? Mathf.Max(0.01f, strength)
                : 1f;

            Vector3 rotationOffset = cue.RotationOffset;
            if (cue.MirrorOnFacing && _source.PlanarForward.z < 0f)
                rotationOffset.y += 180f;

            return new VFXPlaySignal(
                systemType: SystemType.Combat,
                vfxType: VFXType.None,
                position: anchor.position,
                rotation: resolveRotation(cue, anchor),
                positionOffset: cue.PositionOffset,
                rotationOffset: rotationOffset,
                scale: scale,
                parent: cue.FollowAnchor ? anchor : null,
                followParent: cue.FollowAnchor,
                oneShot: true,
                lifetime: cue.Lifetime,
                instanceId: 0,
                playbackRate: playbackRate,
                startDelay: 0f,
                prefab: cue.Prefab);
        }

        private Quaternion resolveRotation(VisualCueEntry cue, Transform anchor)
        {
            if (cue.Alignment == VisualAlignment.PlanarSwingArc)
                return resolvePlanarArcRotation(anchor);

            Vector3 direction = cue.Alignment switch
            {
                VisualAlignment.BladeDirection => _source.BladeDirection,
                VisualAlignment.SwingVelocity => _source.SwingVelocity,
                VisualAlignment.FacingForward => _source.PlanarForward,
                _ => Vector3.zero
            };

            // A swing sampled on a frame where the weapon happens to be still
            // gives a zero vector; falling back to the blade's own pose keeps
            // the arc pointing somewhere sensible instead of snapping to world
            // forward.
            if (direction.sqrMagnitude < 1e-6f &&
                cue.Alignment == VisualAlignment.SwingVelocity)
            {
                direction = _source.BladeDirection;
            }

            if (direction.sqrMagnitude < 1e-6f)
                return anchor.rotation;

            return Quaternion.LookRotation(direction.normalized, Vector3.up);
        }

        /// <summary>
        /// Faces the arc out of the gameplay plane and rolls it along the cut.
        ///
        /// The plane normal is +X because traversal freezes X and the camera
        /// watches the YZ plane from that side. If the camera ever moves to the
        /// other side, this constant is the one thing that changes.
        /// </summary>
        private Quaternion resolvePlanarArcRotation(Transform anchor)
        {
            Vector3 cut = _source.SwingVelocity;

            if (cut.sqrMagnitude < 1e-6f)
                cut = _source.BladeDirection;

            if (cut.sqrMagnitude < 1e-6f)
                return anchor.rotation;

            // Flatten onto the gameplay plane so a stray X component cannot
            // tilt the arc away from the camera.
            cut.x = 0f;

            if (cut.sqrMagnitude < 1e-6f)
                return anchor.rotation;

            return Quaternion.LookRotation(Vector3.right, cut.normalized);
        }

        /// <summary>
        /// Maps how fast the weapon was moving onto the entry's strength range.
        /// Clamped at both ends: a stationary weapon still produces a visible
        /// effect, and a wild swing cannot produce an absurd one.
        /// </summary>
        private float resolveSwingStrength(VisualCueEntry cue)
        {
            if (cue.ScaleBy != VisualScalar.SwingSpeed &&
                cue.RateBy != VisualScalar.SwingSpeed)
            {
                return 1f;
            }

            float speed = _source.SwingVelocity.magnitude;

            float min = Mathf.Min(cue.SwingSpeedRange.x, cue.SwingSpeedRange.y);
            float max = Mathf.Max(cue.SwingSpeedRange.x, cue.SwingSpeedRange.y);
            float t = Mathf.Approximately(max, min)
                ? 1f
                : Mathf.Clamp01((speed - min) / (max - min));

            return Mathf.Lerp(
                cue.SwingStrengthRange.x,
                cue.SwingStrengthRange.y,
                t);
        }

        private void warnUnknownCue(string cueKey)
        {
            if (!_warnOnUnknownCue || string.IsNullOrWhiteSpace(cueKey))
                return;

            if (!_warned.Add(cueKey))
                return;

            Debug.LogWarning(
                $"[WeaponVisualizer] Clip fired cue '{cueKey}' but the " +
                $"visual pack '{_pack.name}' does not list it.",
                _pack);
        }
    }
}
