using System.Collections.Generic;
using UnityEngine;

public readonly struct PlanarWeaponHit
{
    public Collider Collider { get; }
    public Vector3 Point { get; }

    public PlanarWeaponHit(Collider collider, Vector3 point)
    {
        Collider = collider;
        Point = point;
    }
}

public class WeaponHitboxSensor : MonoBehaviour
{
    public Vector3 Position => _hitboxCenter != null
        ? _hitboxCenter.position
        : transform.position;
    public Vector3 BladeDirection => getPlanarDirection(
        _hitboxCenter != null
            ? _hitboxCenter.forward
            : transform.forward
    );
    public Vector3 Velocity { get; private set; }

    /// <summary>
    /// The transform the damage volume is actually centred on. Effects anchor
    /// here rather than on this component's own object, which may sit anywhere
    /// on the weapon.
    /// </summary>
    public Transform Center => _hitboxCenter != null ? _hitboxCenter : transform;

    /// <summary>
    /// Far end of the damage volume along the blade, in world space. The
    /// volume is authored as half extents around its centre with the blade
    /// running along local Z, so the tip is one half-length out that way.
    /// </summary>
    public Vector3 TipPosition =>
        Center.TransformPoint(new Vector3(0f, 0f, _halfExtents.z));

    [SerializeField] private Transform _hitboxCenter;
    [SerializeField] private Vector3 _halfExtents =
        new Vector3(0.5f, 0.5f, 0.5f);
    [SerializeField] private LayerMask _targetLayer;
    [SerializeField, Min(1f)]
    private float _planarBroadphaseHalfDepth = 1000f;

    private const float PlanarEpsilon = 1e-6f;

    [Tooltip("How far the blade may travel in one frame and still be treated " +
        "as having swung there. Beyond this it is taken to have been moved " +
        "rather than swung - a respawn or a weapon swap - and the sweep is " +
        "dropped for that frame rather than covering the whole jump.")]
    [SerializeField, Min(0f)] private float _maxSweepDistance = 4f;

    [Tooltip("Maximum distance covered between two samples by traversal " +
        "attacks such as dash-stab. Their body is moved in FixedUpdate while " +
        "damage is evaluated in Update, so several physics steps may need to " +
        "be represented by one continuous weapon sweep.")]
    [SerializeField, Min(0f)] private float _maxTraversalSweepDistance = 16f;

    private readonly List<PlanarWeaponHit> _hits = new();
    private readonly List<Vector2> _projectedCorners = new(16);
    private readonly List<Vector2> _previousCorners = new(8);
    private Vector3 _previousSweepCenter;
    private bool _sweepValid;
    private int _lastScanFrame = -1;
    private readonly List<Vector2> _bladePolygon = new(8);
    private readonly List<Vector2> _clipA = new(12);
    private readonly List<Vector2> _clipB = new(12);

    /// <summary>
    /// Signed rate the blade is turning in the gameplay plane, radians per
    /// second. Positive turns from +Z toward +Y.
    ///
    /// Deliberately separate from <see cref="Velocity"/>. A lunging attack
    /// moves the whole character, and that travel swamps the much smaller
    /// sideways component a sweep shows up in, so reading the swing's
    /// direction off the linear velocity flips its sign whenever the character
    /// happens to be moving faster than the blade sweeps sideways. The blade's
    /// own angle cannot be contaminated that way.
    /// </summary>
    public float AngularVelocity { get; private set; }

    /// <summary>
    /// Frames the turn rate is measured over. One frame is too noisy to decide
    /// which way a stroke trails; this is about 50ms at 60fps, short enough to
    /// still be the current swing.
    /// </summary>
    private const int AngleWindow = 4;

    private readonly float[] _angleSamples = new float[AngleWindow];
    private readonly float[] _angleTimes = new float[AngleWindow];
    private int _angleIndex;
    private int _angleCount;
    private float _unwrappedAngle;

    private Vector3 _previousPosition;

    private enum ClipBoundary
    {
        MinimumX,
        MaximumX,
        MinimumY,
        MaximumY
    }

    private void OnEnable()
    {
        _previousPosition = Position;
        Velocity = Vector3.zero;

        AngularVelocity = 0f;
        _angleIndex = 0;
        _angleCount = 0;

        // A weapon that has just been enabled has no history to sweep from.
        _sweepValid = false;
        _lastScanFrame = -1;
    }

    private void LateUpdate()
    {
        Vector3 currentPosition = Position;
        Vector3 planarDelta = currentPosition - _previousPosition;
        planarDelta.x = 0f;

        Velocity = Time.deltaTime > Mathf.Epsilon
            ? planarDelta / Time.deltaTime
            : Vector3.zero;

        _previousPosition = currentPosition;

        sampleTurnRate();

        // Keep one render-frame of pose history even while no damage window
        // is open. A short window can open and close inside a single rendered
        // frame; its opening scan still needs the pose from immediately before
        // the lunge in order to cover what the blade crossed.
        recordSweepPose(Center);
    }

    /// <summary>
    /// Pushes this frame's blade angle into the window and reads the turn rate
    /// back out across it.
    /// </summary>
    private void sampleTurnRate()
    {
        Vector3 blade = BladeDirection;

        // A frame where the blade's heading cannot be read tells us nothing;
        // keeping the last rate is better than reporting a stop.
        if (blade.sqrMagnitude < PlanarEpsilon)
            return;

        float angle = Mathf.Atan2(blade.y, blade.z);

        if (_angleCount == 0)
        {
            _unwrappedAngle = angle;
        }
        else
        {
            // A swing that crosses the wrap point would otherwise read as most
            // of a turn the other way, which is exactly the sign we care about.
            float delta =
                Mathf.Repeat(angle - _unwrappedAngle + Mathf.PI, 2f * Mathf.PI) - Mathf.PI;
            _unwrappedAngle += delta;
        }

        _angleSamples[_angleIndex] = _unwrappedAngle;
        _angleTimes[_angleIndex] = Time.time;
        _angleIndex = (_angleIndex + 1) % AngleWindow;
        _angleCount = Mathf.Min(_angleCount + 1, AngleWindow);

        int oldest = (_angleIndex - _angleCount + AngleWindow) % AngleWindow;
        float span = Time.time - _angleTimes[oldest];

        if (span > 1e-4f)
            AngularVelocity = (_unwrappedAngle - _angleSamples[oldest]) / span;
    }

    /// <summary>
    /// Resolves this frame's hits, and remembers the pose so the next frame
    /// can sweep from it.
    /// </summary>
    public IReadOnlyList<PlanarWeaponHit> ScanHits(
        bool traversalSweep = false) =>
        scan(
            true,
            traversalSweep
                ? _maxTraversalSweepDistance
                : _maxSweepDistance
        );

    /// <summary>
    /// The same resolution without touching the sweep, for drawing what the
    /// scan would find. Gizmos run on their own schedule and several times a
    /// frame, and letting them advance the sweep would mean the picture
    /// changed what it was drawing.
    /// </summary>
    private IReadOnlyList<PlanarWeaponHit> peekHits() =>
        scan(false, _maxSweepDistance);

    private IReadOnlyList<PlanarWeaponHit> scan(
        bool advanceSweep,
        float maximumSweepDistance)
    {
        _hits.Clear();

        Transform hitbox = Center;
        buildProjectedBladePolygon(
            hitbox,
            canSweepFrom(hitbox, maximumSweepDistance)
        );

        if (advanceSweep)
            recordSweepPose(hitbox);

        if (_bladePolygon.Count < 3)
            return _hits;

        getPlanarBounds(
            _bladePolygon,
            out Vector2 planarMinimum,
            out Vector2 planarMaximum
        );

        Vector2 planarCenter =
            (planarMinimum + planarMaximum) * 0.5f;
        Vector2 planarExtents =
            (planarMaximum - planarMinimum) * 0.5f;
        // Centred on the plane rather than on the blade. PhysX is only asked
        // which colliders are near the swing in Y and Z; letting the box
        // follow the weapon's own depth would make the broad phase reach
        // further on whichever side the animation happened to throw the arm.
        Vector3 queryCenter = new Vector3(
            GameplayPlane.Depth,
            planarCenter.y,
            planarCenter.x
        );
        Vector3 queryHalfExtents = new Vector3(
            _planarBroadphaseHalfDepth,
            Mathf.Max(planarExtents.y, PlanarEpsilon),
            Mathf.Max(planarExtents.x, PlanarEpsilon)
        );

        // PhysX is used only as a broad phase. Whether a hit exists and where
        // it happened are both resolved below in the authoritative Y-Z plane.
        Collider[] candidates = Physics.OverlapBox(
            queryCenter,
            queryHalfExtents,
            Quaternion.identity,
            _targetLayer,
            QueryTriggerInteraction.Collide
        );

        foreach (Collider candidate in candidates)
        {
            if (candidate == null ||
                !tryGetPlanarContact(candidate, out Vector3 point))
            {
                continue;
            }

            _hits.Add(new PlanarWeaponHit(candidate, point));
        }

        return _hits;
    }

    /// <summary>
    /// The shape the blade covered since the last frame, in plane
    /// coordinates.
    /// </summary>
    /// <remarks>
    /// The hull of where the blade is and where it was, rather than only where
    /// it is. A sword at the middle of a swing crosses more ground in one
    /// frame than a character is thick, so an instantaneous test samples the
    /// blade on either side of the target and reports nothing - the faster the
    /// attack, the more likely it is to pass straight through. That failure is
    /// worst exactly when the player is most certain they connected.
    ///
    /// Taking the hull over both poses is the cheap standard answer: it is
    /// still one convex polygon, so the clip below is unchanged, and it is
    /// conservative in the right direction - it can only ever be larger than
    /// the true swept region, never smaller.
    /// </remarks>
    private void buildProjectedBladePolygon(Transform hitbox, bool sweep)
    {
        _projectedCorners.Clear();
        addProjectedCorners(hitbox, _projectedCorners);

        if (sweep)
            _projectedCorners.AddRange(_previousCorners);

        _projectedCorners.Sort(comparePlanarPoints);
        removeDuplicateProjectedCorners(_projectedCorners);
        buildConvexHull(_projectedCorners, _bladePolygon);
    }

    /// <summary>
    /// Whether the last recorded pose is a pose this one actually swung from.
    /// </summary>
    /// <remarks>
    /// Two ways it is not. The scans may not be back to back - the hit window
    /// closed and reopened, and in between the blade went through a whole
    /// return-to-idle that it never cut anything with. Or the blade moved
    /// further than a swing reaches, which means it was placed rather than
    /// swung. Either way the hull would cover ground the weapon never
    /// travelled, and everything standing in it would take a hit.
    /// </remarks>
    private bool canSweepFrom(
        Transform hitbox,
        float maximumSweepDistance)
    {
        int framesSinceSample = Time.frameCount - _lastScanFrame;
        if (!_sweepValid ||
            framesSinceSample < 0 ||
            framesSinceSample > 1)
        {
            return false;
        }

        Vector3 centre = GameplayPlane.Flatten(hitbox.position);
        float allowedDistance = Mathf.Max(0f, maximumSweepDistance);

        return (centre - _previousSweepCenter).sqrMagnitude <=
            allowedDistance * allowedDistance;
    }

    private void addProjectedCorners(Transform hitbox, List<Vector2> corners)
    {
        for (int x = -1; x <= 1; x += 2)
        {
            for (int y = -1; y <= 1; y += 2)
            {
                for (int z = -1; z <= 1; z += 2)
                {
                    Vector3 localCorner = new Vector3(
                        x * _halfExtents.x,
                        y * _halfExtents.y,
                        z * _halfExtents.z
                    );
                    corners.Add(toPlanar(hitbox.TransformPoint(localCorner)));
                }
            }
        }
    }

    /// <summary>
    /// Keeps this scan's blade pose for the next scan to sweep back to.
    /// </summary>
    private void recordSweepPose(Transform hitbox)
    {
        _previousCorners.Clear();
        addProjectedCorners(hitbox, _previousCorners);

        _previousSweepCenter = GameplayPlane.Flatten(hitbox.position);
        _sweepValid = true;
        _lastScanFrame = Time.frameCount;
    }

    private bool tryGetPlanarContact(
        Collider target,
        out Vector3 contactPoint)
    {
        Bounds bounds = target.bounds;
        Vector2 targetMinimum = toPlanar(bounds.min);
        Vector2 targetMaximum = toPlanar(bounds.max);

        _clipA.Clear();
        _clipA.AddRange(_bladePolygon);

        List<Vector2> input = _clipA;
        List<Vector2> output = _clipB;

        if (!clipPolygon(
                ref input,
                ref output,
                ClipBoundary.MinimumX,
                targetMinimum.x) ||
            !clipPolygon(
                ref input,
                ref output,
                ClipBoundary.MaximumX,
                targetMaximum.x) ||
            !clipPolygon(
                ref input,
                ref output,
                ClipBoundary.MinimumY,
                targetMinimum.y) ||
            !clipPolygon(
                ref input,
                ref output,
                ClipBoundary.MaximumY,
                targetMaximum.y))
        {
            contactPoint = default;
            return false;
        }

        Vector2 planarContact = Vector2.zero;
        foreach (Vector2 point in input)
            planarContact += point;

        planarContact /= input.Count;

        // On the plane, not on the target. This is the value knockback,
        // torque lever arms and slice planes are all taken from, so it has to
        // be the same point for the same overlap however far the model it hit
        // happens to be sitting off the plane. Presentation can move an
        // effect back onto the mesh downstream if it wants to.
        contactPoint = fromPlanar(planarContact);
        return true;
    }

    private static bool clipPolygon(
        ref List<Vector2> input,
        ref List<Vector2> output,
        ClipBoundary boundary,
        float boundaryValue)
    {
        output.Clear();

        if (input.Count == 0)
            return false;

        Vector2 start = input[input.Count - 1];
        bool startInside = isInside(start, boundary, boundaryValue);

        foreach (Vector2 end in input)
        {
            bool endInside = isInside(end, boundary, boundaryValue);

            if (endInside != startInside)
            {
                output.Add(getBoundaryIntersection(
                    start,
                    end,
                    boundary,
                    boundaryValue
                ));
            }

            if (endInside)
                output.Add(end);

            start = end;
            startInside = endInside;
        }

        List<Vector2> swap = input;
        input = output;
        output = swap;
        return input.Count > 0;
    }

    private static bool isInside(
        Vector2 point,
        ClipBoundary boundary,
        float value)
    {
        return boundary switch
        {
            ClipBoundary.MinimumX => point.x >= value,
            ClipBoundary.MaximumX => point.x <= value,
            ClipBoundary.MinimumY => point.y >= value,
            ClipBoundary.MaximumY => point.y <= value,
            _ => false
        };
    }

    private static Vector2 getBoundaryIntersection(
        Vector2 start,
        Vector2 end,
        ClipBoundary boundary,
        float value)
    {
        Vector2 delta = end - start;
        bool isVerticalBoundary = boundary == ClipBoundary.MinimumX ||
            boundary == ClipBoundary.MaximumX;
        float denominator = isVerticalBoundary ? delta.x : delta.y;

        if (Mathf.Abs(denominator) <= PlanarEpsilon)
            return start;

        float startValue = isVerticalBoundary ? start.x : start.y;
        float interpolation = (value - startValue) / denominator;
        return start + delta * interpolation;
    }

    private static void buildConvexHull(
        List<Vector2> points,
        List<Vector2> hull)
    {
        hull.Clear();

        if (points.Count <= 2)
        {
            hull.AddRange(points);
            return;
        }

        foreach (Vector2 point in points)
        {
            while (hull.Count >= 2 &&
                cross(
                    hull[hull.Count - 2],
                    hull[hull.Count - 1],
                    point) <= PlanarEpsilon)
            {
                hull.RemoveAt(hull.Count - 1);
            }

            hull.Add(point);
        }

        int lowerHullCount = hull.Count;

        for (int i = points.Count - 2; i >= 0; i--)
        {
            Vector2 point = points[i];

            while (hull.Count > lowerHullCount &&
                cross(
                    hull[hull.Count - 2],
                    hull[hull.Count - 1],
                    point) <= PlanarEpsilon)
            {
                hull.RemoveAt(hull.Count - 1);
            }

            hull.Add(point);
        }

        if (hull.Count > 1)
            hull.RemoveAt(hull.Count - 1);
    }

    private static void removeDuplicateProjectedCorners(
        List<Vector2> points)
    {
        for (int i = points.Count - 1; i > 0; i--)
        {
            if ((points[i] - points[i - 1]).sqrMagnitude <=
                PlanarEpsilon * PlanarEpsilon)
            {
                points.RemoveAt(i);
            }
        }
    }

    private static void getPlanarBounds(
        List<Vector2> polygon,
        out Vector2 minimum,
        out Vector2 maximum)
    {
        minimum = polygon[0];
        maximum = polygon[0];

        for (int i = 1; i < polygon.Count; i++)
        {
            minimum = Vector2.Min(minimum, polygon[i]);
            maximum = Vector2.Max(maximum, polygon[i]);
        }
    }

    private static int comparePlanarPoints(Vector2 left, Vector2 right)
    {
        int horizontalComparison = left.x.CompareTo(right.x);
        return horizontalComparison != 0
            ? horizontalComparison
            : left.y.CompareTo(right.y);
    }

    private static float cross(
        Vector2 origin,
        Vector2 first,
        Vector2 second)
    {
        Vector2 a = first - origin;
        Vector2 b = second - origin;
        return a.x * b.y - a.y * b.x;
    }

    private static Vector2 toPlanar(Vector3 point) => GameplayPlane.ToPlanar(point);

    private static Vector3 fromPlanar(Vector2 point) => GameplayPlane.FromPlanar(point);

    private static Vector3 getPlanarDirection(Vector3 direction) =>
        GameplayPlane.FlattenDirection(direction);

    private void OnDrawGizmosSelected()
    {
        Transform hitbox = _hitboxCenter != null
            ? _hitboxCenter
            : transform;

        Gizmos.color = new Color(1f, 0.2f, 0.2f, 0.35f);

        Matrix4x4 previousMatrix = Gizmos.matrix;
        Gizmos.matrix = Matrix4x4.TRS(
            hitbox.position,
            hitbox.rotation,
            hitbox.lossyScale
        );
        Gizmos.DrawWireCube(Vector3.zero, _halfExtents * 2f);
        Gizmos.matrix = previousMatrix;

        IReadOnlyList<PlanarWeaponHit> planarHits = peekHits();

        // Drawn where the test happens, not where the weapon is. The two
        // parting company is the whole class of bug this plane exists to
        // prevent, so the gizmo must not quietly hide it.
        float displayPlaneX = GameplayPlane.Depth;

        Gizmos.color = Color.cyan;
        drawPlanarPolygon(_bladePolygon, displayPlaneX);

        foreach (PlanarWeaponHit planarHit in planarHits)
        {
            Collider target = planarHit.Collider;
            if (target == null || target.transform.root == transform.root)
                continue;

            drawProjectedTargetBounds(target.bounds, displayPlaneX);

            Vector3 displayPoint = new Vector3(
                displayPlaneX,
                planarHit.Point.y,
                planarHit.Point.z
            );
            Gizmos.color = Color.green;
            Gizmos.DrawSphere(displayPoint, 0.06f);
            Gizmos.DrawLine(hitbox.position, displayPoint);
        }
    }

    private static void drawPlanarPolygon(
        List<Vector2> polygon,
        float planeX)
    {
        if (polygon.Count < 2)
            return;

        for (int i = 0; i < polygon.Count; i++)
        {
            Vector3 start = GameplayPlane.FromPlanarAtDepth(polygon[i], planeX);
            Vector3 end = GameplayPlane.FromPlanarAtDepth(
                polygon[(i + 1) % polygon.Count],
                planeX
            );
            Gizmos.DrawLine(start, end);
        }
    }

    private static void drawProjectedTargetBounds(
        Bounds bounds,
        float planeX)
    {
        Vector3 minimum = new Vector3(
            planeX,
            bounds.min.y,
            bounds.min.z
        );
        Vector3 maximum = new Vector3(
            planeX,
            bounds.max.y,
            bounds.max.z
        );
        Vector3 topLeft = new Vector3(
            planeX,
            maximum.y,
            minimum.z
        );
        Vector3 bottomRight = new Vector3(
            planeX,
            minimum.y,
            maximum.z
        );

        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(minimum, topLeft);
        Gizmos.DrawLine(topLeft, maximum);
        Gizmos.DrawLine(maximum, bottomRight);
        Gizmos.DrawLine(bottomRight, minimum);
    }
}
