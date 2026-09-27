using System.Collections.Generic;
using UnityEngine;

namespace Combat
{
    /// <summary>
    /// One slash in the world.
    ///
    /// Every frame while the swing runs, the visible blade is read off the rig
    /// as a segment and pushed onto a spine; the mesh is the surface that spine
    /// sweeps. The stroke therefore is the swept path, on the axis the blade
    /// really travelled, and its leading cross-section is the blade itself, so
    /// it meets the weapon by construction.
    ///
    /// Samples are kept in three dimensions. An earlier version flattened them
    /// onto the gameplay plane, which made the stroke follow the projection of
    /// the swing rather than the swing, and left it with no thickness for
    /// anything to be offset through. Both of those are load-bearing here: the
    /// volume shells stack along the sweep's own normal, and the particles are
    /// laid along the tip's own heading.
    ///
    /// The object's own transform is a frame, not a follower. It is placed once
    /// so the mesh's numbers stay small and local, and then never moves.
    /// </summary>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class SlashEffect : MonoBehaviour
    {
        private static readonly int CoreColorId = Shader.PropertyToID("_CoreColor");
        private static readonly int EdgeColorId = Shader.PropertyToID("_EdgeColor");
        private static readonly int GlowColorId = Shader.PropertyToID("_GlowColor");
        private static readonly int BrightnessId = Shader.PropertyToID("_Brightness");
        private static readonly int CoreSharpnessId = Shader.PropertyToID("_CoreSharpness");
        private static readonly int GlowIntensityId = Shader.PropertyToID("_GlowIntensity");
        private static readonly int FadeId = Shader.PropertyToID("_Fade");
        private static readonly int DriftPhaseId = Shader.PropertyToID("_DriftPhase");

        private static readonly int LifeId = Shader.PropertyToID("_Life");

        /// <summary>
        /// Where along its own texture the cut's head is. The band's u is
        /// distance from the start, so a layered material that shades by
        /// position along the stroke reads this to put the thick, hot end on
        /// the blade however far the swing has got. Materials that do not
        /// declare it ignore it.
        /// </summary>
        private static readonly int HeadUId = Shader.PropertyToID("_HeadU");

        /// <summary>
        /// Tint on the pack's particle materials. The ring is usually drawn
        /// with one of those, and they have no dissolve to drive, so this is
        /// the only handle its fade has.
        /// </summary>
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        /// <summary>
        /// Grace period before a still blade ends the stroke. A cue can fire a
        /// frame or two before the swing itself starts moving.
        /// </summary>
        private const float MinEmitSeconds = 0.05f;

        /// <summary>
        /// Tip speed under which the blade counts as still, in units per
        /// second. A swing runs an order of magnitude above this; carrying a
        /// weapon while walking stays well under it.
        /// </summary>
        private const float StillSpeed = 1.5f;

        /// <summary>How long the blade has to stay still before the swing is over.</summary>
        private const float StallSeconds = 0.05f;

        /// <summary>
        /// Ceiling on particles laid in one frame, per system. A single long
        /// frame - a hitch, a breakpoint - would otherwise dump a whole swing's
        /// worth at once.
        /// </summary>
        private const int MaxParticlesPerFrame = 24;

        private MeshFilter _filter;
        private MeshRenderer _renderer;
        private MaterialPropertyBlock _block;
        private Mesh _mesh;

        private readonly List<SlashRibbonBuilder.Sample> _spine = new(64);

        /// <summary>
        /// Every blade the swing has passed through, from the first frame on.
        ///
        /// The spine beside it is a moving window - the ribbon is the blur
        /// behind a travelling edge, so its oldest end retires as the edge
        /// leaves. The cut is not a blur and does not retire that way: it is
        /// drawn once, whole, and then erodes. Two lists, because they are two
        /// different things that happen to be sampled from the same blade.
        /// </summary>
        private readonly List<SlashShapeBuilder.Sample> _arc = new(96);

        private SlashProfile _profile;
        private IWeaponVisualSource _source;
        private VisualAnchor _pivotAnchor;
        private VisualAnchor _tipAnchor;

        private Vector3 _origin;
        private Quaternion _toLocal;

        private GameObject _particleRoot;
        private ParticleSystem[] _particles = System.Array.Empty<ParticleSystem>();
        private float[] _particleAuthoredCount = System.Array.Empty<float>();
        private float[] _particleShare = System.Array.Empty<float>();
        private float[] _particleDebt = System.Array.Empty<float>();
        private bool[] _particleShapes = System.Array.Empty<bool>();
        private bool[] _particleOriented = System.Array.Empty<bool>();
        private GameObject _particleSource;

        private GameObject _shapeRoot;
        private MeshRenderer _shapeRenderer;
        private MaterialPropertyBlock _shapeBlock;
        private Mesh _shapeMesh;
        private GameObject _shapeSource;
        private int _shapeBurst = -1;
        private Material _shapeRing;
        private bool _shapeCircle;

        /// <summary>
        /// The head's u from the last band built, held through the fade so the
        /// shading stays put while the cut erodes. One for the ring, whose u
        /// already runs tail to head across a single frame.
        /// </summary>
        private float _shapeHeadU = 1f;

        /// <summary>
        /// The ring Circle mode is struck on, read once on the stroke's first
        /// frame. Only its sweep moves afterwards.
        /// </summary>
        private SlashCircleBuilder.Frame _circle;
        private bool _circleReady;

        /// <summary>
        /// Radians the blade has turned about the ring's axis, accumulated
        /// across frames so a swing past the half turn keeps climbing instead
        /// of wrapping.
        /// </summary>
        private float _sweep;
        private float _lastSweepAngle;
        private bool _windingLatched;

        /// <summary>
        /// How far the tip has reached from the ring's centre, over the
        /// stroke's own window. Both readings are kept because the profile
        /// chooses between them, and neither can be recovered from the other
        /// after the fact.
        /// </summary>
        private float _reachMax;
        private float _reachSum;
        private int _reachCount;

        /// <summary>
        /// How far the blade has to have turned, in radians, before the swing
        /// is taken to have a direction. Small enough to be settled within a
        /// frame or two of a real swing, wide enough that a rig jittering
        /// about its start pose cannot set it.
        /// </summary>
        private const float WindingThreshold = 0.05f;

        private bool _emitting;
        private float _emitTime;
        private float _stallTime;
        private float _fadeTime;
        private Vector3 _lastTip;
        private float _shapeLife;

        /// <summary>Free to be played again.</summary>
        public bool IsIdle => !gameObject.activeSelf;

        /// <summary>
        /// The cue that started this stroke, so a VisualCueEnd naming the same
        /// cue can find it again.
        /// </summary>
        public string CueKey { get; private set; }

        /// <summary>
        /// The animator state whose clip started this stroke.
        ///
        /// Combo clips overlap: the next attack's start cue fires while the
        /// previous clip is still running out its own events, so an end cue
        /// late in one clip can arrive after the next clip's stroke has begun.
        /// Recording the state lets an end cue speak only for its own clip, the
        /// same guard the combat machine already applies to hit frames.
        /// </summary>
        public string StateName { get; private set; }

        /// <summary>Still laying down new trail.</summary>
        public bool IsEmitting => _emitting;

        /// <summary>
        /// Stops the stroke growing, and nothing else.
        /// </summary>
        /// <remarks>
        /// What is already drawn is untouched: it keeps ageing out of the
        /// trail's window and fading on its own schedule. Cutting it off here
        /// instead would pop the stroke out of existence mid-swing, which is
        /// the one thing an end cue must not do - the point of ending early is
        /// to stop the weapon dragging a fresh trail behind it, not to erase
        /// the trail it has already cut.
        /// </remarks>
        public void StopEmitting() => _emitting = false;

        public static SlashEffect Create(string name, Material material)
        {
            var go = new GameObject(name);
            var effect = go.AddComponent<SlashEffect>();
            effect.GetComponent<MeshRenderer>().sharedMaterial = material;
            go.SetActive(false);
            return effect;
        }

        private void Awake()
        {
            _filter = GetComponent<MeshFilter>();
            _renderer = GetComponent<MeshRenderer>();
            _renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _renderer.receiveShadows = false;
            _renderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            _renderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;

            _block = new MaterialPropertyBlock();
            _mesh = new Mesh { name = "SlashRibbon" };
            _mesh.MarkDynamic();
            _filter.sharedMesh = _mesh;
        }

        private void OnDestroy()
        {
            if (_mesh != null)
                Destroy(_mesh);

            if (_shapeMesh != null)
                Destroy(_shapeMesh);
        }

        /// <summary>
        /// Starts a stroke on the weapon <paramref name="source"/> describes.
        /// </summary>
        /// <param name="cueKey">The cue that started it, for a later end cue to name.</param>
        /// <param name="stateName">The animator state that started it, likewise.</param>
        /// <param name="pivotAnchor">Grip end of the weapon.</param>
        /// <param name="tipAnchor">Far end of the weapon.</param>
        /// <param name="planeNormal">Gameplay plane normal, used only to orient the frame.</param>
        public void Play(
            SlashProfile profile,
            string cueKey,
            string stateName,
            IWeaponVisualSource source,
            VisualAnchor pivotAnchor,
            VisualAnchor tipAnchor,
            Vector3 planeNormal)
        {
            // The material only matters to the generated ribbon; a profile that
            // leaves the drawing to an authored effect needs no material.
            if (profile == null || source == null ||
                (profile.DrawRibbon && profile.Material == null))
            {
                return;
            }

            _profile = profile;
            CueKey = cueKey;
            StateName = stateName;
            _source = source;
            _pivotAnchor = pivotAnchor;
            _tipAnchor = tipAnchor;

            if (!readBlade(out Vector3 grip, out Vector3 tip))
                return;

            if (profile.Material != null && _renderer.sharedMaterial != profile.Material)
                _renderer.sharedMaterial = profile.Material;

            // Somewhere to hang the mesh, so its coordinates stay small and
            // near the action. Set once; the mesh does all the moving.
            Quaternion rotation = Quaternion.LookRotation(planeNormal, Vector3.up);
            _origin = grip;
            _toLocal = Quaternion.Inverse(rotation);
            transform.SetPositionAndRotation(_origin, rotation);

            _spine.Clear();
            _arc.Clear();
            _shapeLife = 1f;
            _emitting = true;
            _emitTime = 0f;
            _stallTime = 0f;
            _fadeTime = 0f;
            _lastTip = tip;

            captureCircle(grip, tip);
            push(grip, tip, Time.time, true);
            ensureParticles(profile);
            ensureShape(profile);

            applyProfile();
            gameObject.SetActive(true);
            enabled = true;
        }

        // LateUpdate so the rig has already been posed by the animator this frame.
        private void LateUpdate()
        {
            float now = Time.time;

            if (_emitting)
                emit(now);
            else
                _fadeTime += Time.deltaTime;

            updateShape();

            // The trail is a moving window over the last few moments, so the
            // oldest end retires on its own whether or not the swing is over.
            float trail = Mathf.Max(_profile.TrailSeconds, 1e-3f);
            while (_spine.Count > 0 && now - _spine[0].Time > trail)
                _spine.RemoveAt(0);

            float fade = _emitting
                ? 1f
                : 1f - Mathf.Clamp01(_fadeTime / Mathf.Max(_profile.FadeSeconds, 1e-3f));

            bool ribbonDone = _spine.Count < 2 || fade <= 0f;

            if (ribbonDone)
            {
                _mesh.Clear();
                _spine.Clear();

                // The particles and the cut both outlive the stroke that
                // laid them. Sleeping now would take their objects down with
                // this one and cut every live one mid-flight.
                if (particlesAlive() || shapeAlive())
                    return;

                _arc.Clear();
                _source = null;
                _emitting = false;
                enabled = false;
                gameObject.SetActive(false);
                return;
            }

            if (_profile.DrawRibbon)
            {
                SlashRibbonBuilder.Build(_profile, _spine, now, _mesh);
                setFade(fade * fade, (1f - fade) * 0.6f);
            }
            else
            {
                _mesh.Clear();
            }
        }

        /// <summary>
        /// Adds this frame's blade to the spine, lays particles along the path
        /// it covered, and decides whether the swing is still running.
        /// </summary>
        private void emit(float now)
        {
            _emitTime += Time.deltaTime;

            if (!readBlade(out Vector3 grip, out Vector3 tip))
            {
                _emitting = false;
                return;
            }

            push(grip, tip, now, false);
            trackSweep(tip);
            emitParticles(_lastTip, tip, grip, Time.deltaTime);

            float speed = Time.deltaTime > 1e-5f
                ? (tip - _lastTip).magnitude / Time.deltaTime
                : 0f;

            _stallTime = speed > StillSpeed ? 0f : _stallTime + Time.deltaTime;

            // A clip that authors its own end cue says when the stroke stops,
            // and this guess must not pre-empt it. Left on, it is the safety
            // net for clips that do not: the blade has stopped moving, so there
            // was nothing left to draw anyway.
            bool stalled = _profile.AutoStopWhenStill
                && _emitTime >= MinEmitSeconds
                && _stallTime >= StallSeconds;

            bool over = stalled || _emitTime >= _profile.FollowSeconds;

            _lastTip = tip;

            if (over)
                _emitting = false;
        }

        #region Circle

        /// <summary>
        /// Reads the ring Circle mode is struck on, once.
        /// </summary>
        /// <remarks>
        /// Everything is taken on this one frame and then held. The gameplay
        /// plane is the ring's plane - the effect's own frame is already built
        /// from that normal, so in local terms the ring lies in XY and its
        /// axis is Z. The blade gives the rest: where it points is angle zero,
        /// how long it is sets the radius, and the grip is the centre, which
        /// in this frame is the origin.
        ///
        /// Held rather than followed because the grip travels its own arc as
        /// the arm swings. A ring that chased it would wobble with the elbow,
        /// and a clean shape is the only thing this mode has over the band.
        /// </remarks>
        private void captureCircle(Vector3 grip, Vector3 tip)
        {
            _circleReady = false;
            _sweep = 0f;
            _lastSweepAngle = 0f;
            _windingLatched = false;

            Vector3 axis = Vector3.forward;
            Vector3 reference = Vector3.ProjectOnPlane(toFrame(tip), axis);

            // A blade pointing straight down the plane's normal has no angle
            // within the plane, so there is no circle to strike.
            if (reference.sqrMagnitude < 1e-8f)
                return;

            // The blade's reach on this one frame, which is only ever a seed:
            // it stops the ring starting at nothing, and the swing itself
            // decides how wide it really is.
            float reach = reference.magnitude;

            _reachMax = reach;
            _reachSum = reach;
            _reachCount = 1;

            _circle = new SlashCircleBuilder.Frame
            {
                Center = Vector3.zero,
                Axis = axis,
                Reference = reference / reach,
                Radius = reach,
                Sweep = 0f,
                Winding = 1f
            };

            _circleReady = true;
        }

        /// <summary>
        /// Advances how far round the ring the blade has got.
        /// </summary>
        /// <remarks>
        /// Measured as a delta and accumulated rather than read straight off
        /// the angle. Read straight off, a swing that passes the half turn
        /// wraps to the far side and drags the stroke's head backwards through
        /// the whole circle in one frame.
        ///
        /// The tip is measured against the ring's fixed centre rather than the
        /// live grip, because the centre is what the ring was struck about;
        /// using the moving grip would mix the arm's travel into an angle that
        /// is only meant to carry the swing's turn.
        /// </remarks>
        private void trackSweep(Vector3 tip)
        {
            if (!_circleReady)
                return;

            Vector3 arm = Vector3.ProjectOnPlane(toFrame(tip) - _circle.Center, _circle.Axis);

            if (arm.sqrMagnitude < 1e-8f)
                return;

            float reach = arm.magnitude;
            arm /= reach;

            _reachMax = Mathf.Max(_reachMax, reach);
            _reachSum += reach;
            _reachCount++;

            // Measured over the stroke rather than taken from the weapon: the
            // blade's length is where the tip started, and by the end of the
            // swing the arm has carried it well past that. Never allowed to
            // fall, so the ring settles on the swing's own circle instead of
            // breathing in and out with the arm.
            float radius = _profile.CircleRadiusSource == SlashCircleRadius.Average
                ? _reachSum / _reachCount
                : _reachMax;

            _circle.Radius = Mathf.Max(_circle.Radius, radius);

            float angle = Mathf.Atan2(
                Vector3.Dot(Vector3.Cross(_circle.Reference, arm), _circle.Axis),
                Vector3.Dot(_circle.Reference, arm));

            _sweep += Mathf.DeltaAngle(
                _lastSweepAngle * Mathf.Rad2Deg,
                angle * Mathf.Rad2Deg) * Mathf.Deg2Rad;

            _lastSweepAngle = angle;
            _circle.Sweep = _sweep;

            // Taken once, on the first frame the blade has really turned, and
            // then held for the rest of the stroke. Recomputed every frame it
            // would flip while the swing was still settling, and the ring's
            // texture would mirror itself under the player.
            if (!_windingLatched && Mathf.Abs(_sweep) > WindingThreshold)
            {
                _circle.Winding = Mathf.Sign(_sweep);
                _windingLatched = true;
            }
        }

        #endregion

        #region Base shape

        /// <summary>
        /// Gives the slash's own shape somewhere to be drawn, and takes the
        /// look it is drawn with from the authored effect.
        /// </summary>
        /// <remarks>
        /// Only the materials are taken. The mesh that came with them was a
        /// ring, a perfect circle put into the world once and then uncovered
        /// by the shader, and a swing is not a circle - so the drawn arc and
        /// the blade agreed at a couple of points and parted company
        /// everywhere else. The band is built from the blade's own path now,
        /// which is what makes it sweep at all.
        /// </remarks>
        private void ensureShape(SlashProfile profile)
        {
            if (profile.ShapeMode == SlashShapeMode.None)
            {
                releaseShape();
                return;
            }

            bool circle = profile.ShapeMode == SlashShapeMode.Circle;
            Material ring = circle ? profile.CircleMaterial : null;

            bool sameSource = _shapeSource == profile.Particles
                && _shapeBurst == profile.ShapeBurstCount
                && _shapeCircle == circle
                && _shapeRing == ring;

            if (_shapeRoot != null && sameSource)
                return;

            _shapeSource = profile.Particles;
            _shapeBurst = profile.ShapeBurstCount;
            _shapeCircle = circle;
            _shapeRing = ring;

            var layers = new List<Material>(2);

            // The ring is one drawn shape rather than an authored stack, so
            // its own material wins outright. Left empty it falls through to
            // the prefab's layers, which is what lets a circle profile be
            // dressed by the same authored effect a band one is.
            if (ring != null)
            {
                layers.Add(ring);
            }

            // In prefab order, so the layers keep the depth the author gave
            // them - the dark backing drawn before the colour that sits on it.
            for (int i = 0; ring == null && i < _particles.Length; i++)
            {
                if (_particleShapes[i] &&
                    _particles[i].TryGetComponent(out ParticleSystemRenderer drawn) &&
                    drawn.sharedMaterial != null)
                {
                    layers.Add(drawn.sharedMaterial);
                }
            }

            if (layers.Count == 0)
            {
                releaseShape();
                return;
            }

            if (_shapeRoot == null)
            {
                _shapeRoot = new GameObject("Shape");
                _shapeRoot.transform.SetParent(transform, false);

                _shapeMesh = new Mesh { name = "SlashShape" };
                _shapeMesh.MarkDynamic();
                _shapeRoot.AddComponent<MeshFilter>().sharedMesh = _shapeMesh;

                _shapeRenderer = _shapeRoot.AddComponent<MeshRenderer>();
                _shapeRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                _shapeRenderer.receiveShadows = false;
                _shapeRenderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
                _shapeRenderer.reflectionProbeUsage =
                    UnityEngine.Rendering.ReflectionProbeUsage.Off;

                _shapeBlock = new MaterialPropertyBlock();
                _shapeRoot.SetActive(false);
            }

            _shapeRenderer.sharedMaterials = layers.ToArray();
        }

        private void releaseShape()
        {
            if (_shapeRoot != null)
                Destroy(_shapeRoot);

            if (_shapeMesh != null)
                Destroy(_shapeMesh);

            _shapeRoot = null;
            _shapeRenderer = null;
            _shapeMesh = null;
            _shapeBlock = null;
            _shapeSource = null;
            _shapeBurst = -1;
            _shapeRing = null;
            _shapeCircle = false;
        }

        /// <summary>
        /// Grows the band with the swing, then lets it erode once the swing is
        /// over.
        /// </summary>
        /// <remarks>
        /// Unlike the ribbon the band keeps the whole stroke rather than a
        /// moving window over the last few moments. The ribbon is the blur
        /// behind a moving edge and has to retire as the edge leaves; this is
        /// the cut itself, and a cut stays until it fades. So it is built from
        /// every sample since the swing began, and what ends it is the
        /// dissolve rather than the trail's clock.
        /// </remarks>
        private void updateShape()
        {
            if (_shapeRoot == null)
                return;

            if (_emitting)
            {
                _shapeLife = 1f;

                // The ring needs no history: it was struck whole on the first
                // frame and only its bright head has moved since, so unlike
                // the band it is on screen from the moment the cue fires.
                bool built = _profile.ShapeMode == SlashShapeMode.Circle
                    ? buildCircle()
                    : buildBand();

                if (built && !_shapeRoot.activeSelf)
                    _shapeRoot.SetActive(true);
            }
            else
            {
                // The band stops growing but is not cut short: what was drawn
                // stays drawn and erodes on its own, the same promise an end
                // cue makes to the trail.
                float fade = Mathf.Max(_profile.ShapeFadeSeconds, 1e-3f);
                _shapeLife = Mathf.Max(_shapeLife - Time.deltaTime / fade, 0f);

                if (_shapeLife <= 0f)
                {
                    _shapeRoot.SetActive(false);
                    _shapeMesh.Clear();
                    return;
                }
            }

            if (!_shapeRoot.activeSelf)
                return;

            _shapeRenderer.GetPropertyBlock(_shapeBlock);
            _shapeBlock.SetFloat(LifeId, _shapeLife);
            _shapeBlock.SetFloat(HeadUId, _shapeHeadU);

            // The pack's particle materials have no dissolve to erode, so the
            // ring fades the only way they can - on the tint they multiply by.
            // Band mode is left alone: an authored layer's own base colour is
            // part of the look its author chose.
            if (_profile.ShapeMode == SlashShapeMode.Circle)
            {
                Color tint = _profile.EdgeColor;
                tint.a *= _shapeLife;
                _shapeBlock.SetColor(BaseColorId, tint);
            }

            _shapeRenderer.SetPropertyBlock(_shapeBlock);
        }

        private bool buildCircle()
        {
            if (!_circleReady)
                return false;

            SlashCircleBuilder.Build(_profile, _circle, _shapeMesh);
            _shapeHeadU = 1f;
            return true;
        }

        private bool buildBand()
        {
            if (_arc.Count < 2)
                return false;

            _shapeHeadU = SlashShapeBuilder.Build(_profile, _arc, _shapeMesh);
            return true;
        }

        /// <summary>True while the cut is still on screen.</summary>
        private bool shapeAlive() => _shapeRoot != null && _shapeRoot.activeSelf;

        #endregion

        #region Particles

        /// <summary>
        /// Brings the profile's particle prefab in as a child, or swaps it when
        /// the profile changed.
        ///
        /// Every system under the prefab is driven, not just its root: an
        /// authored effect is usually several systems layered, and taking only
        /// one of them would quietly drop most of the look.
        ///
        /// Each has its own emission and shape switched off. The stroke knows
        /// where the blade went and the prefab does not, so every particle is
        /// placed here; the prefab keeps only its look - its colour, its size,
        /// its lifetime, its trails.
        /// </summary>
        private void ensureParticles(SlashProfile profile)
        {
            if (profile.Particles == null)
            {
                clearParticles();
                return;
            }

            if (_particleRoot == null || _particleSource != profile.Particles)
            {
                clearParticles();

                _particleRoot = Instantiate(profile.Particles, transform);
                _particleRoot.name = "Particles";
                _particleRoot.transform.localPosition = Vector3.zero;
                _particleRoot.transform.localRotation = Quaternion.identity;

                _particleSource = profile.Particles;
                _particles = _particleRoot.GetComponentsInChildren<ParticleSystem>(true);
                _particleAuthoredCount = new float[_particles.Length];
                _particleShare = new float[_particles.Length];
                _particleDebt = new float[_particles.Length];
                _particleShapes = new bool[_particles.Length];
                _particleOriented = new bool[_particles.Length];

                for (int i = 0; i < _particles.Length; i++)
                {
                    ParticleSystem.MainModule main = _particles[i].main;

                    // Only a mesh particle has an orientation worth setting. A
                    // billboard turns to the camera whatever it is told, so
                    // writing a 3D rotation into one only costs work.
                    _particleOriented[i] =
                        _particles[i].TryGetComponent(out ParticleSystemRenderer drawn) &&
                        drawn.renderMode == ParticleSystemRenderMode.Mesh;

                    if (_particleOriented[i])
                    {
                        main.startRotation3D = true;

                        // One source of orientation. Left on Local, a mesh also
                        // inherits the emitter's own pose, and the prefab's
                        // systems are each turned on their side - so two of the
                        // five would end up rotated differently from the rest.
                        drawn.alignment = ParticleSystemRenderSpace.World;
                    }

                    // World space, so a particle stays in the air it was laid
                    // into rather than riding the effect's frame.
                    main.simulationSpace = ParticleSystemSimulationSpace.World;
                    main.playOnAwake = false;
                    main.loop = true;

                    ParticleSystem.EmissionModule emission = _particles[i].emission;

                    // Read what the author asked for before switching it off.
                    // It is the only record of what each system was meant to
                    // be, and telling the two kinds apart depends on it.
                    _particleAuthoredCount[i] = authoredCount(emission);
                    emission.enabled = false;

                    ParticleSystem.ShapeModule shape = _particles[i].shape;
                    shape.enabled = false;
                }
            }

            // Outside the branch above: a stroke is pooled and every profile
            // points at this same prefab, so what a profile asks for has to be
            // worked out per play rather than once when the prefab came in.
            sortSystems(profile);

            for (int i = 0; i < _particles.Length; i++)
            {
                _particleDebt[i] = 0f;

                if (!_particles[i].isPlaying)
                    _particles[i].Play(false);
            }
        }

        /// <summary>
        /// How many particles a system was authored to fire in one go. Its
        /// bursts if it has any, otherwise its rate.
        /// </summary>
        private static float authoredCount(ParticleSystem.EmissionModule emission)
        {
            int count = emission.burstCount;

            if (count <= 0)
                return Mathf.Max(emission.rateOverTime.constantMax, 1f);

            var bursts = new ParticleSystem.Burst[count];
            emission.GetBursts(bursts);

            float total = 0f;
            for (int i = 0; i < count; i++)
                total += bursts[i].count.constantMax;

            return Mathf.Max(total, 1f);
        }

        /// <summary>
        /// Decides which of the prefab's systems are the slash's own shape and
        /// which are the spray around it, and how often the spray is laid.
        /// </summary>
        /// <remarks>
        /// The two cannot be driven the same way, and trying to was the whole
        /// trouble. A system authored to fire one particle is not a sparse
        /// spray - it is the slash itself, one mesh drawn the width of the
        /// entire arc, carrying a dissolve timed to its own lifetime. Lay forty
        /// of those along the swing and not one of the forty is ever the shape:
        /// they overlap into a band, each caught at a different instant of its
        /// own dissolve and turned a different way, and a single clean cut
        /// becomes a smear. So the shape is laid once, square to the plane the
        /// blade actually swept, and only the spray trails.
        /// </remarks>
        private void sortSystems(SlashProfile profile)
        {
            float largest = 0f;

            for (int i = 0; i < _particles.Length; i++)
            {
                _particleShapes[i] = _particleAuthoredCount[i] <= profile.ShapeBurstCount;

                if (!_particleShapes[i])
                    largest = Mathf.Max(largest, _particleAuthoredCount[i]);
            }

            // The spray systems keep their balance against one another. That
            // is a ratio between systems of the same kind, so unlike the one
            // between a spray and a shape it survives being laid along a path.
            for (int i = 0; i < _particles.Length; i++)
            {
                _particleShare[i] = largest > 0f
                    ? Mathf.Clamp01(_particleAuthoredCount[i] / largest)
                    : 1f;
            }
        }

        private void clearParticles()
        {
            if (_particleRoot != null)
                Destroy(_particleRoot);

            _particleRoot = null;
            _particleSource = null;
            _particles = System.Array.Empty<ParticleSystem>();
            _particleAuthoredCount = System.Array.Empty<float>();
            _particleShare = System.Array.Empty<float>();
            _particleDebt = System.Array.Empty<float>();
            _particleShapes = System.Array.Empty<bool>();
            _particleOriented = System.Array.Empty<bool>();
        }

        /// <summary>
        /// Lays the spray along the segment the tip just covered.
        ///
        /// The count is per unit travelled rather than per frame, so the
        /// density follows how hard the swing was and not the frame rate.
        /// </summary>
        private void emitParticles(Vector3 from, Vector3 to, Vector3 grip, float deltaTime)
        {
            if (_particles.Length == 0 || _profile.ParticlesPerUnit <= 0f)
                return;

            Vector3 step = to - from;
            float travel = step.magnitude;

            if (travel < 1e-5f)
                return;

            Vector3 heading = step / travel;
            float speed = deltaTime > 1e-5f ? travel / deltaTime : 0f;
            Vector3 facing = sweepRotation(to - grip, heading);

            float opacity = Mathf.Clamp01(_profile.ParticleOpacity);
            float lifetime = _profile.ParticleLifetime;

            for (int s = 0; s < _particles.Length; s++)
            {
                if (_particleShapes[s])
                    continue;

                ParticleSystem system = _particles[s];
                ParticleSystem.MainModule main = system.main;

                // Each system keeps its own tally. The remainder is carried, so
                // a slow frame does not round its particles away and a fast one
                // does not double up - and a system on a small share still gets
                // its particle every few frames instead of never.
                _particleDebt[s] += travel * _profile.ParticlesPerUnit * _particleShare[s];

                int count = Mathf.FloorToInt(_particleDebt[s]);
                if (count <= 0)
                    continue;

                _particleDebt[s] -= count;
                count = Mathf.Min(count, MaxParticlesPerFrame);

                for (int i = 0; i < count; i++)
                {
                    var emit = new ParticleSystem.EmitParams
                    {
                        position = Vector3.Lerp(from, to, (i + 0.5f) / count),
                        velocity = heading * (speed * _profile.ParticleSpeedScale)
                            + Random.insideUnitSphere * _profile.ParticleSpread,
                        applyShapeToPosition = false,
                        startSize = sampleStartSize(main) * _profile.ParticleSizeScale
                    };

                    // Left alone, a one-shot effect's sparks outlive the trail
                    // that laid them by several seconds, so a swing's debris is
                    // still hanging in the air two swings later.
                    if (lifetime > 0f)
                        emit.startLifetime = lifetime;

                    if (opacity < 1f)
                    {
                        Color colour = sampleStartColor(main);
                        colour.a *= opacity;
                        emit.startColor = colour;
                    }

                    if (_particleOriented[s] && _profile.AlignParticlesToSweep)
                        emit.rotation3D = facing;

                    system.Emit(emit, 1);
                }
            }
        }

        /// <summary>True while any laid particle is still alive.</summary>
        private bool particlesAlive()
        {
            for (int i = 0; i < _particles.Length; i++)
            {
                if (_particles[i] != null && _particles[i].IsAlive(false))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// The pose that lays a mesh particle flat in the plane the blade is
        /// sweeping through, as euler angles.
        ///
        /// A mesh particle's own +Z is its face, so pointing that along the
        /// sweep's normal puts the shape in the swing's plane; pointing its +Y
        /// along the blade then rolls it to the cut. Without this every laid
        /// mesh keeps the pose it was modelled in - for these rings, lying flat
        /// on the ground - which is why they came out facing nowhere.
        /// </summary>
        private Vector3 sweepRotation(Vector3 blade, Vector3 heading)
        {
            Vector3 normal = Vector3.Cross(blade, heading);

            // A blade travelling straight along its own length sweeps no plane.
            if (normal.sqrMagnitude < 1e-10f || blade.sqrMagnitude < 1e-10f)
                return _profile.ParticleRotationOffset;

            Quaternion pose =
                Quaternion.LookRotation(normal.normalized, blade.normalized) *
                Quaternion.Euler(_profile.ParticleRotationOffset);

            return pose.eulerAngles;
        }

        private static float sampleStartSize(ParticleSystem.MainModule main)
        {
            ParticleSystem.MinMaxCurve size = main.startSize;

            return size.mode switch
            {
                ParticleSystemCurveMode.TwoConstants =>
                    Random.Range(size.constantMin, size.constantMax),
                ParticleSystemCurveMode.Constant => size.constant,
                _ => size.constantMax * size.curveMultiplier
            };
        }

        /// <summary>
        /// A system's own start colour, so dimming a laid particle scales what
        /// the author chose instead of replacing it.
        /// </summary>
        private static Color sampleStartColor(ParticleSystem.MainModule main)
        {
            ParticleSystem.MinMaxGradient colour = main.startColor;

            switch (colour.mode)
            {
                case ParticleSystemGradientMode.Color:
                    return colour.color;

                case ParticleSystemGradientMode.TwoColors:
                    return Color.Lerp(colour.colorMin, colour.colorMax, Random.value);

                case ParticleSystemGradientMode.Gradient:
                    return colour.gradient != null ? colour.gradient.Evaluate(0f) : Color.white;

                case ParticleSystemGradientMode.RandomColor:
                    return colour.gradient != null
                        ? colour.gradient.Evaluate(Random.value)
                        : Color.white;

                case ParticleSystemGradientMode.TwoGradients:
                    if (colour.gradientMin == null || colour.gradientMax == null)
                        return Color.white;

                    return Color.Lerp(
                        colour.gradientMin.Evaluate(0f),
                        colour.gradientMax.Evaluate(0f),
                        Random.value);

                default:
                    return Color.white;
            }
        }

        #endregion
        /// <summary>
        /// Moves the spine's leading vertebra onto the blade, fixing the one
        /// behind it in place once the blade has travelled far enough to be
        /// worth keeping.
        ///
        /// The head slides rather than being appended every frame, which is
        /// what keeps the ribbon's leading edge exactly on the weapon between
        /// vertebrae instead of a frame behind it.
        /// </summary>
        private void push(Vector3 grip, Vector3 tip, float time, bool force)
        {
            Vector3 outer = toFrame(tip);
            Vector3 root = toFrame(grip);
            Vector3 inner = Vector3.Lerp(root, outer, _profile.StartAlongBlade);

            var head = new SlashRibbonBuilder.Sample
            {
                Inner = inner,
                Outer = outer,
                Time = time
            };

            var reach = new SlashShapeBuilder.Sample { Grip = root, Tip = outer };

            // The decision is taken on the arc rather than the spine, because
            // the spine loses its oldest samples to the trail's window and a
            // list that keeps shrinking cannot say whether the blade moved.
            int count = _arc.Count;
            bool append = force || count < 2;

            if (!append)
            {
                SlashShapeBuilder.Sample previous = _arc[count - 1];
                SlashShapeBuilder.Sample anchor = _arc[count - 2];

                float step = _profile.MinStep * _profile.MinStep;

                append =
                    (previous.Tip - anchor.Tip).sqrMagnitude >= step ||
                    (previous.Grip - anchor.Grip).sqrMagnitude >= step;
            }

            if (append)
            {
                _spine.Add(head);
                _arc.Add(reach);
                return;
            }

            // Otherwise the leading vertebra slides onto the blade instead of
            // a new one being laid, which is what keeps both leading edges
            // exactly on the weapon between samples rather than a frame behind.
            if (_spine.Count > 0)
                _spine[_spine.Count - 1] = head;
            else
                _spine.Add(head);

            _arc[count - 1] = reach;
        }

        /// <summary>
        /// Into the effect's own frame. A rotation and a translation only - the
        /// point keeps all three of its dimensions, because the swing has all
        /// three and the stroke is meant to follow the swing.
        /// </summary>
        private Vector3 toFrame(Vector3 world) => _toLocal * (world - _origin);

        /// <summary>Reads the visible blade's two ends from the rig.</summary>
        private bool readBlade(out Vector3 grip, out Vector3 tip)
        {
            grip = Vector3.zero;
            tip = Vector3.zero;

            // A stroke outlives the frame its weapon is destroyed on, and the
            // interface hides the destroyed-object check the cast brings back.
            if (_source == null || (_source is Object weapon && weapon == null))
                return false;

            return _source.TryGetAnchorPosition(_pivotAnchor, out grip) &&
                   _source.TryGetAnchorPosition(_tipAnchor, out tip) &&
                   (tip - grip).sqrMagnitude > 1e-8f;
        }

        private void applyProfile()
        {
            _renderer.GetPropertyBlock(_block);

            _block.SetColor(CoreColorId, _profile.CoreColor);
            _block.SetColor(EdgeColorId, _profile.EdgeColor);
            _block.SetColor(GlowColorId, _profile.GlowColor);
            _block.SetFloat(BrightnessId, _profile.Brightness);
            _block.SetFloat(CoreSharpnessId, _profile.CoreSharpness);
            _block.SetFloat(GlowIntensityId, _profile.GlowIntensity);
            _block.SetFloat(FadeId, 1f);
            _block.SetFloat(DriftPhaseId, 0f);

            _renderer.SetPropertyBlock(_block);
        }

        private void setFade(float fade, float driftPhase)
        {
            _renderer.GetPropertyBlock(_block);
            _block.SetFloat(FadeId, fade);
            _block.SetFloat(DriftPhaseId, driftPhase);
            _renderer.SetPropertyBlock(_block);
        }
    }
}
