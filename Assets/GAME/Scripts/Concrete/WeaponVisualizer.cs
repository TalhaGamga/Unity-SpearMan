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
        /// Pack key for the burst on a confirmed hit. "Hit.&lt;AttackKey&gt;"
        /// overrides it for one attack, so a heavy finisher can land harder
        /// than a jab without the pack needing an entry for every attack.
        /// </summary>
        private const string HitCueKey = "Hit";

        /// <summary>
        /// How far in front of the struck body an impact is drawn, in world
        /// units along the plane normal. The contact itself lies on the plane,
        /// inside the body, where the body's own mesh would depth-hide it;
        /// just past the camera-facing side of its bounds it sits on the
        /// silhouette instead.
        /// </summary>
        private const float ImpactTowardCamera = 0.06f;

        /// <summary>
        /// How far from the struck body's spine an impact may sit, towards
        /// the contact, in world units. Enough to show which side the blade
        /// came in from; little enough that an overhead cut's burst stays on
        /// the chest, well below the chin, instead of on the head.
        /// </summary>
        private const float ImpactTorsoReach = 0.12f;

        /// <summary>
        /// The lowest the impact may sit on the hips-to-chest segment, as a
        /// fraction from the hips: never below mid-spine. The hips are where
        /// the attacker's own legs and blade are on a low cut or a cut into a
        /// body lying at their feet, and a burst there lands on the hero's
        /// shin rather than on the target.
        /// </summary>
        private const float ImpactSpineLow = 0.5f;

        /// <summary>
        /// How far, in degrees, the impact's direction may be turned from the
        /// sensor's reading towards the swing's own tangent at the point it is
        /// drawn. The sensor reads the edge at first contact - the top of an
        /// upright body on an overhead cut, where the edge still runs level -
        /// and the burst is then moved down to the torso, where the blade is
        /// already coming down. Capped, so a bad lever never spins the cut
        /// round.
        /// </summary>
        private const float ImpactLeanDegrees = 45f;

        /// <summary>
        /// Turn rate, radians per second, below which the blade's rotation
        /// says nothing reliable about the swing's tangent - the same floor
        /// the sword applies to its own reading.
        /// </summary>
        private const float ImpactMinTurnRate = 0.5f;

        /// <summary>
        /// For a target with no humanoid rig: the band of its bounds' height,
        /// as fractions from the bottom, the contact is held to - the torso of
        /// a body, never its top.
        /// </summary>
        private const float ImpactTorsoLow = 0.3f;
        private const float ImpactTorsoHigh = 0.62f;

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

        /// <summary>
        /// The stroke whose damage window combat currently has open, if any -
        /// the one a confirmed hit belongs to.
        /// </summary>
        private SlashEffect _windowSlash;

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
            _windowSlash = null;
        }

        /// <summary>
        /// Combat opened or closed its hit scan for the named state.
        /// </summary>
        /// <remarks>
        /// Driven by the combat machine rather than by the clip's own
        /// HitFrameOpen/HitFrameClose events, because the two disagree exactly
        /// when it matters. Combat checks a window's clip against the state it
        /// is in and closes the scan when it leaves that state; a clip being
        /// crossfaded out keeps firing its events regardless. Cancel the
        /// rising cut into the finisher before its window opens and the old
        /// clip still fires HitFrameOpen - a window combat throws away, which
        /// on the raw event would have drawn a swing that cannot hurt anything
        /// as the thick, hot band that says it does.
        ///
        /// The stroke is matched by the state that started it, so a window
        /// lights only the swing it belongs to, and only while it is still
        /// being drawn.
        /// </remarks>
        public void HandleHitWindow(bool open, string stateName)
        {
            if (!open)
            {
                if (_windowSlash != null)
                    _windowSlash.SetHitWindow(false);

                _windowSlash = null;
                return;
            }

            if (_windowSlash != null)
                _windowSlash.SetHitWindow(false);

            _windowSlash = findEmittingSlash(stateName);

            if (_windowSlash != null)
                _windowSlash.SetHitWindow(true);
        }

        /// <summary>
        /// The stroke still being drawn for the named state, newest first.
        /// </summary>
        private SlashEffect findEmittingSlash(string stateName)
        {
            SlashEffect found = null;

            for (int i = 0; i < _slashes.Count; i++)
            {
                SlashEffect effect = _slashes[i];

                if (effect == null || !effect.IsEmitting || !matches(effect.StateName, stateName))
                    continue;

                if (found == null || effect.StartedAt > found.StartedAt)
                    found = effect;
            }

            return found;
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

        #region Hit

        /// <summary>
        /// Second entry point, for a hit the weapon has just confirmed.
        ///
        /// Driven by the hit scan rather than by a clip, which is the whole
        /// point: a clip fires the same frames whether or not the swing
        /// connected, so an impact hung off an animation event would burst on
        /// every whiff. This fires only for a target the rules are about to
        /// act on, which is also what lets a miss show no impact at all.
        ///
        /// A pack with no hit cue simply shows nothing. The lookup stays
        /// silent, unlike a clip cue, because nothing was mistyped - the pack
        /// has just not been given an impact yet.
        /// </summary>
        public void HandleHit(in WeaponHit hit)
        {
            if (_pack == null || _source == null)
                return;

            if (!tryGetHitCue(hit.Attack, out VisualCueEntry cue) ||
                cue.Prefab == null)
            {
                return;
            }

            Vector3 scale = cue.Scale;
            if (cue.ScaleBy == VisualScalar.SwingSpeed)
                scale *= swingStrengthAt(cue, hit.SwingSpeed);

            // The impact is about to take the frame, so the stroke that landed
            // it steps its edge down. Only here, past the cue check: a pack
            // with no impact keeps the hot edge, which is then the only thing
            // saying the cut connected.
            if (_windowSlash != null)
                _windowSlash.ConfirmHit();

            // Placed first, so the direction can be read where it is drawn.
            Vector3 position = resolveImpactPosition(hit);

            // An impact tuned to last longer than the one its cue was timed
            // for would be taken back mid-fade, and the pool clears whatever
            // is still on screen when it takes an instance back - so the hold
            // stretches to fit. Never shorter: the cue's own time is a floor.
            float lifetime = cue.Lifetime;
            if (lifetime > 0f && cue.Prefab.TryGetComponent(out ImpactTuning tuning))
                lifetime = Mathf.Max(lifetime, tuning.SecondsNeeded());

            _play.OnNext(new VFXPlaySignal(
                systemType: SystemType.Combat,
                // Tag only: the prefab below is authoritative, so this never
                // depends on the scene having a VFXSet that lists HitSpark.
                vfxType: VFXType.HitSpark,
                position: position,
                rotation: resolveImpactRotation(hit, position),
                // Unparented, so the manager adds this in world space.
                positionOffset: cue.PositionOffset,
                // Applied by the manager after the rotation, so the prefab
                // ends up at LookRotation(...) * Euler(RotationOffset).
                rotationOffset: cue.RotationOffset,
                scale: scale,
                // Left where it was struck: the target may be launched,
                // knocked down or sliced in half the same frame, and a burst
                // that followed it would smear across the screen.
                parent: null,
                followParent: false,
                oneShot: true,
                lifetime: lifetime,
                instanceId: 0,
                playbackRate: 1f,
                startDelay: 0f,
                prefab: cue.Prefab));
        }

        /// <summary>
        /// The attack's own impact if the pack has one, otherwise the shared
        /// one.
        /// </summary>
        private bool tryGetHitCue(AttackDefinition attack, out VisualCueEntry cue)
        {
            if (attack != null &&
                !string.IsNullOrWhiteSpace(attack.Key) &&
                _pack.TryGetCue($"{HitCueKey}.{attack.Key}", out cue))
            {
                return true;
            }

            return _pack.TryGetCue(HitCueKey, out cue);
        }

        /// <summary>
        /// The contact on the plane, brought out along the plane normal to
        /// just past the struck collider's camera-facing side.
        ///
        /// The contact is computed on the plane, which runs through the middle
        /// of the body, so drawn where it is the body's own mesh would hide it.
        /// The collider's bounds give the side facing the camera without
        /// assuming anything about its shape. Read along PlaneNormal rather
        /// than off world X directly, so the camera moving to the other side
        /// still only means changing that one constant.
        /// </summary>
        private static Vector3 resolveImpactPosition(in WeaponHit hit)
        {
            Vector3 position = resolveImpactAnchor(hit);

            if (hit.Collider == null)
                return position;

            Bounds bounds = hit.Collider.bounds;
            Vector3 reach = new Vector3(
                Mathf.Abs(PlaneNormal.x),
                Mathf.Abs(PlaneNormal.y),
                Mathf.Abs(PlaneNormal.z));

            // Support of the box along the normal: how far out its face on
            // the camera side lies.
            float face = Vector3.Dot(bounds.center, PlaneNormal) +
                Vector3.Dot(bounds.extents, reach);
            float depth = Vector3.Dot(position, PlaneNormal);

            return position + PlaneNormal * (face + ImpactTowardCamera - depth);
        }

        /// <summary>
        /// Where on the plane the impact goes: on the struck body's torso, as
        /// near to the contact as <see cref="ImpactTorsoReach"/> allows.
        ///
        /// The contact itself is the first point of the target's collider the
        /// blade reached, and for a character that is the wrong place to draw
        /// anything. An overhead cut meets the top of an upright capsule, so
        /// the burst sat on the head and erased it; and the capsule stays
        /// upright while the model is launched or knocked flat, so on a downed
        /// enemy the burst hung in the air where the capsule's top was. The
        /// rig's own spine is where the body really is, in whatever pose it
        /// is in, and the short reach keeps a hint of where the blade came in.
        /// Only its upper half (<see cref="ImpactSpineLow"/>): a low cut, or a
        /// cut into a body lying at the attacker's feet, would otherwise put
        /// the burst on the hips, right against the attacker's own legs.
        ///
        /// A target with no humanoid rig falls back to its bounds - the posed
        /// meshes' if it has any, which follow a body lying down, otherwise
        /// the collider's - with the contact held to the band between the
        /// hips and the chest, never the top.
        /// </summary>
        private static Vector3 resolveImpactAnchor(in WeaponHit hit)
        {
            Vector3 point = hit.Point;
            GameObject target = hit.Target;

            if (target != null)
            {
                Animator animator = target.GetComponentInChildren<Animator>();

                if (animator != null && animator.isHuman &&
                    tryTorso(animator, out Vector3 low, out Vector3 high))
                {
                    // Nearest point of the upper half of the hips-to-chest
                    // segment, then back out towards the contact by no more
                    // than the reach.
                    Vector3 contact = GameplayPlane.Flatten(point);
                    Vector3 spine = high - low;
                    float t = Mathf.Clamp(
                        Vector3.Dot(contact - low, spine) / Mathf.Max(spine.sqrMagnitude, 1e-6f),
                        ImpactSpineLow,
                        1f);
                    Vector3 onSpine = low + spine * t;

                    Vector3 anchored = onSpine + Vector3.ClampMagnitude(contact - onSpine, ImpactTorsoReach);
                    anchored.x = point.x;
                    return anchored;
                }
            }

            if (!tryBodyBounds(hit, out Bounds body))
                return point;

            point.y = Mathf.Clamp(
                point.y,
                body.min.y + ImpactTorsoLow * body.size.y,
                body.min.y + ImpactTorsoHigh * body.size.y);
            return point;
        }

        /// <summary>The rig's hips and chest (or spine), on the plane.</summary>
        private static bool tryTorso(Animator animator, out Vector3 low, out Vector3 high)
        {
            low = high = Vector3.zero;

            Transform hips = animator.GetBoneTransform(HumanBodyBones.Hips);
            Transform chest = animator.GetBoneTransform(HumanBodyBones.Chest);

            if (chest == null)
                chest = animator.GetBoneTransform(HumanBodyBones.Spine);

            if (hips == null || chest == null)
                return false;

            low = GameplayPlane.Flatten(hips.position);
            high = GameplayPlane.Flatten(chest.position);
            return true;
        }

        /// <summary>
        /// The bounds of what the player sees of the target: its enabled
        /// skinned meshes if it has any, otherwise the struck collider.
        /// </summary>
        private static bool tryBodyBounds(in WeaponHit hit, out Bounds body)
        {
            body = default;
            bool found = false;

            if (hit.Target != null)
            {
                foreach (SkinnedMeshRenderer skin in hit.Target.GetComponentsInChildren<SkinnedMeshRenderer>())
                {
                    if (!skin.enabled || !skin.gameObject.activeInHierarchy)
                        continue;

                    if (found)
                    {
                        body.Encapsulate(skin.bounds);
                    }
                    else
                    {
                        body = skin.bounds;
                        found = true;
                    }
                }
            }

            if (!found && hit.Collider != null)
            {
                body = hit.Collider.bounds;
                found = true;
            }

            return found;
        }

        /// <summary>
        /// The same framing as PlanarSwingArc: facing the camera, local +Y
        /// along the cut, so an impact authored with its streak along +Y lies
        /// across the target the way the blade went through it.
        ///
        /// The sensor's direction is the edge's at first contact, and the
        /// burst is drawn somewhere else - on the torso, not where the blade
        /// first touched the body. So the direction is leaned, by no more
        /// than <see cref="ImpactLeanDegrees"/>, towards the way a point of
        /// the swing at <paramref name="at"/> is travelling: the edge rotated
        /// a quarter turn about the grip, the same convention the sword reads
        /// its edge with. An overhead cut then marks the chest along the
        /// downward stroke the arc shows beside it, instead of level across
        /// it. A turn too slow to trust, a grip that cannot be read, or a
        /// tangent that points back against the sensor leaves the sensor's
        /// direction as it was.
        /// </summary>
        private Quaternion resolveImpactRotation(in WeaponHit hit, Vector3 at)
        {
            Vector3 along = flatten(hit.SlashDirection);

            if (along.sqrMagnitude < 1e-6f)
                along = flatten(_source.PlanarForward);

            float turn = _source.SwingAngularVelocity;

            if (along.sqrMagnitude > 1e-6f &&
                Mathf.Abs(turn) > ImpactMinTurnRate &&
                _source.TryGetAnchorPosition(VisualAnchor.Hand, out Vector3 grip))
            {
                Vector3 lever = flatten(at - grip);
                Vector3 tangent = new Vector3(0f, lever.z, -lever.y) * Mathf.Sign(turn);

                if (tangent.sqrMagnitude > 1e-4f &&
                    Vector3.Dot(tangent.normalized, along.normalized) > -0.9f)
                {
                    along = Vector3.RotateTowards(
                        along.normalized,
                        tangent.normalized,
                        ImpactLeanDegrees * Mathf.Deg2Rad,
                        0f);
                }
            }

            return along.sqrMagnitude > 1e-6f
                ? Quaternion.LookRotation(PlaneNormal, along.normalized)
                : Quaternion.identity;
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

            return swingStrengthAt(cue, _source.SwingVelocity.magnitude);
        }

        /// <summary>
        /// The mapping itself, for a speed already in hand - a hit carries the
        /// speed sampled at the contact, which is the one that should decide
        /// how hard it looks.
        /// </summary>
        private static float swingStrengthAt(VisualCueEntry cue, float speed)
        {
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
