using System.Collections.Generic;
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
        /// <summary>Authored on a clip to begin an effect.</summary>
        private const string CueStartEventKey = "VisualCueStart";

        /// <summary>
        /// Authored on a clip to stop an effect growing, narrowed by whichever
        /// of Cue and StateName the event carries.
        ///
        /// It belongs where the blade finishes its cut, a frame or two after
        /// the hit window closes - not beside SlashEnd, which the same clips
        /// fire a second or more later to say the whole attack is over. By then
        /// the stroke has long since finished drawing and an end cue there
        /// would appear to do nothing.
        /// </summary>
        private const string CueEndEventKey = "VisualCueEnd";

        /// <summary>
        /// What the start cue was called before it had an end to pair with.
        /// Still honoured so a clip authored against the old name keeps working.
        /// </summary>
        private const string LegacyCueEventKey = "VisualCue";

        /// <summary>
        /// Enough strokes for a combo to overlap without any of them being cut
        /// short, and few enough that a stuck cue cannot flood the scene.
        /// </summary>
        private const int MaxLiveSlashes = 6;

        /// <summary>
        /// Normal of the gameplay plane, pointing at the camera. Traversal
        /// freezes X and the camera watches the YZ plane from that side. If the
        /// camera ever moves to the other side, this constant is the one thing
        /// that changes.
        /// </summary>
        private static readonly Vector3 PlaneNormal = Vector3.right;

        [Tooltip("This weapon's cue dictionary. Shared freely between weapon " +
            "variants that should look the same.")]
        [SerializeField] private WeaponVisualPack _pack;

        [Tooltip("Log once per unknown cue key. Leave on while authoring - a " +
            "typo in a clip event is otherwise completely silent.")]
        [SerializeField] private bool _warnOnUnknownCue = true;

        private readonly Subject<VFXPlaySignal> _play = new();
        private readonly HashSet<string> _warned = new();
        private readonly List<SlashEffect> _slashes = new(MaxLiveSlashes);

        private IWeaponVisualSource _source;
        private int _nextSlash;

        public Observable<VFXPlaySignal> PlayStream => _play;

        public void Init(IWeaponVisualSource source)
        {
            _source = source;
            _warned.Clear();
        }

        public void End()
        {
            _source = null;

            for (int i = 0; i < _slashes.Count; i++)
            {
                if (_slashes[i] != null)
                    Object.Destroy(_slashes[i].gameObject);
            }

            _slashes.Clear();
            _nextSlash = 0;
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

            // An end cue needs no dictionary lookup: it names something already
            // playing, so a cue key the pack has never heard of still ends it.
            if (matches(frame.EventKey, CueEndEventKey))
            {
                stopSlashes(frame.Cue, frame.StateName);
                return;
            }

            if (!matches(frame.EventKey, CueStartEventKey) &&
                !matches(frame.EventKey, LegacyCueEventKey))
            {
                return;
            }

            if (!_pack.TryGetCue(frame.Cue, out VisualCueEntry cue))
            {
                warnUnknownCue(frame.Cue);
                return;
            }

            // A slash cue runs the stroke and then falls through: a prefab on
            // the same cue is an extra accent, spawned at the same instant.
            if (cue.Kind == VisualCueKind.Slash)
                playSlash(cue, frame.StateName);

            if (cue.Prefab == null)
                return;

            if (!_source.TryGetAnchor(cue.Anchor, out Transform anchor) ||
                anchor == null)
            {
                return;
            }

            _play.OnNext(buildSignal(cue, anchor));
        }

        #region Slash

        private void playSlash(VisualCueEntry cue, string stateName)
        {
            SlashProfile profile = cue.SlashProfile;

            // The material only draws the generated ribbon. A profile that
            // turns the ribbon off and leaves the stroke to its particle
            // prefab has none, and needs none - the same rule SlashEffect.Play
            // applies - so only a ribbon without a material is refused.
            if (profile == null || (profile.DrawRibbon && profile.Material == null))
                return;

            if (!_source.TryGetAnchorPosition(cue.Anchor, out Vector3 pivot) ||
                !_source.TryGetAnchorPosition(cue.ArcTip, out Vector3 tip))
            {
                return;
            }

            if ((tip - pivot).sqrMagnitude < 1e-8f)
                return;

            // A combo can transition out of a clip before its end cue is
            // reached: in two of the attack clips the combo window opens while
            // the cut is still running, so cancelling into the next attack
            // skips the end cue entirely. A new cut from a different clip says
            // the previous one is over just as plainly.
            stopSupersededSlashes(stateName);

            SlashEffect effect = rentSlash(profile.Material);
            if (effect == null)
                return;

            // No sweep direction to resolve: the stroke is the path the blade
            // takes, so which way it goes is simply where the blade goes.
            effect.Play(
                profile,
                cue.CueKey,
                stateName,
                _source,
                cue.Anchor,
                cue.ArcTip,
                PlaneNormal);
        }

        /// <summary>
        /// Stops the named strokes growing without disturbing what they have
        /// already drawn.
        ///
        /// An end cue is about the weapon no longer cutting, not about the air
        /// it cut being tidied away, so the strokes stay on screen and age out
        /// the way they always would.
        ///
        /// Both names narrow what is ended, and a clip normally gives both.
        /// The state matters most: combo clips overlap, so an end cue placed
        /// late in one attack can arrive after the next attack's stroke has
        /// already started, and without the state gate it would cut that fresh
        /// stroke short. This is the same guard the combat machine applies to
        /// hit frames, for the same reason.
        /// </summary>
        /// <summary>
        /// Stops strokes left emitting by a clip the animator has moved on
        /// from, without touching ones the current clip started.
        ///
        /// Two cues in the same clip may legitimately overlap, so only a
        /// different state supersedes. A stroke that never recorded a state is
        /// left alone: nothing here can tell whether it belongs to this clip.
        /// </summary>
        private void stopSupersededSlashes(string stateName)
        {
            if (string.IsNullOrWhiteSpace(stateName))
                return;

            for (int i = 0; i < _slashes.Count; i++)
            {
                SlashEffect effect = _slashes[i];

                if (effect == null || !effect.IsEmitting)
                    continue;

                if (string.IsNullOrWhiteSpace(effect.StateName) ||
                    matches(effect.StateName, stateName))
                {
                    continue;
                }

                effect.StopEmitting();
            }
        }

        private void stopSlashes(string cueKey, string stateName)
        {
            bool anyCue = string.IsNullOrWhiteSpace(cueKey);
            bool anyState = string.IsNullOrWhiteSpace(stateName);

            for (int i = 0; i < _slashes.Count; i++)
            {
                SlashEffect effect = _slashes[i];

                if (effect == null || !effect.IsEmitting)
                    continue;

                if (!anyCue && !matches(effect.CueKey, cueKey))
                    continue;

                // An effect that never recorded a state cannot be excluded by
                // one, so a cue-only end still reaches it.
                if (!anyState &&
                    !string.IsNullOrWhiteSpace(effect.StateName) &&
                    !matches(effect.StateName, stateName))
                {
                    continue;
                }

                effect.StopEmitting();
            }
        }

        /// <summary>
        /// Cue and event keys are hand-typed into clip events and into a pack,
        /// in two different windows, often months apart.
        /// </summary>
        private static bool matches(string a, string b) =>
            string.Equals(a, b, System.StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// An idle stroke if there is one, a new one while the pool is small,
        /// and otherwise the oldest - a combo fast enough to exhaust the pool
        /// is better served by cutting its first stroke than by dropping its
        /// latest.
        /// </summary>
        private SlashEffect rentSlash(Material material)
        {
            for (int i = 0; i < _slashes.Count; i++)
            {
                if (_slashes[i] != null && _slashes[i].IsIdle)
                    return _slashes[i];
            }

            if (_slashes.Count < MaxLiveSlashes)
            {
                var created = SlashEffect.Create(
                    $"Slash ({_pack.name}) {_slashes.Count}", material);
                _slashes.Add(created);
                return created;
            }

            SlashEffect oldest = _slashes[_nextSlash];
            _nextSlash = (_nextSlash + 1) % _slashes.Count;
            return oldest;
        }

        #endregion

        #region Prefab

        private VFXPlaySignal buildSignal(VisualCueEntry cue, Transform anchor)
        {
            float strength = resolveSwingStrength(cue);

            Vector3 scale = cue.Scale;
            if (cue.ScaleBy == VisualScalar.SwingSpeed)
                scale *= strength;

            Quaternion rotation;
            if (cue.Alignment == VisualAlignment.BladeTrailArc)
            {
                rotation = resolveTrailArc(cue, anchor, out float arcScale);
                scale *= arcScale;
            }
            else
            {
                rotation = resolveRotation(cue, anchor);
            }

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
                rotation: rotation,
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
            // the effect pointing somewhere sensible instead of snapping to
            // world forward.
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
        /// Faces a flat effect out of the gameplay plane and rolls it along the
        /// cut, so a side camera sees the whole of it instead of a sliver.
        /// </summary>
        private Quaternion resolvePlanarArcRotation(Transform anchor)
        {
            Vector3 cut = flatten(_source.SwingVelocity);

            if (cut.sqrMagnitude < 1e-6f)
                cut = flatten(_source.BladeDirection);

            if (cut.sqrMagnitude < 1e-6f)
                return anchor.rotation;

            return Quaternion.LookRotation(PlaneNormal, cut.normalized);
        }

        /// <summary>
        /// Places an authored ring prefab as the trail of the blade tip: its
        /// origin on the pivot, its outer edge through the tip, its leading
        /// edge on the blade and its tail behind the swing.
        /// </summary>
        private Quaternion resolveTrailArc(
            VisualCueEntry cue, Transform pivot, out float arcScale)
        {
            arcScale = 1f;

            Vector3 blade = Vector3.zero;
            if (_source.TryGetAnchorPosition(cue.ArcTip, out Vector3 tip))
                blade = flatten(tip - pivot.position);

            float radius = blade.magnitude;

            if (radius < 1e-3f)
            {
                blade = flatten(_source.BladeDirection);
                radius = cue.ArcRadius;
            }

            if (blade.sqrMagnitude < 1e-6f)
                return resolvePlanarArcRotation(pivot);

            Vector3 bladeDirection = blade.normalized;

            if (cue.ArcRadius > 1e-4f)
                arcScale = radius / cue.ArcRadius;

            float leadRadians = cue.ArcLeadAngle * Mathf.Deg2Rad;
            var lead = new Vector3(Mathf.Cos(leadRadians), Mathf.Sin(leadRadians), 0f);
            var tailTangent = new Vector3(-lead.y, lead.x, 0f);
            if (cue.ArcWinding == VisualArcWinding.TowardNegativeAngle)
                tailTangent = -tailTangent;

            Quaternion unframe =
                Quaternion.Inverse(Quaternion.LookRotation(Vector3.forward, lead));
            Quaternion rotation =
                Quaternion.LookRotation(PlaneNormal, bladeDirection) * unframe;

            Vector3 travel = flatten(_source.SwingVelocity);
            if (travel.sqrMagnitude < 1e-6f)
                travel = flatten(_source.PlanarForward);

            if (travel.sqrMagnitude > 1e-6f &&
                Vector3.Dot(rotation * tailTangent, travel) > 0f)
            {
                rotation =
                    Quaternion.LookRotation(-PlaneNormal, bladeDirection) * unframe;
            }

            return rotation;
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

        #endregion

        /// <summary>
        /// Drops the component along the plane normal so a stray sideways
        /// component cannot tilt an effect away from the camera.
        /// </summary>
        private static Vector3 flatten(Vector3 v) =>
            Vector3.ProjectOnPlane(v, PlaneNormal);

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
