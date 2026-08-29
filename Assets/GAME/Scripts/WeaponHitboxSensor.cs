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

    [SerializeField] private Transform _hitboxCenter;
    [SerializeField] private Vector3 _halfExtents =
        new Vector3(0.5f, 0.5f, 0.5f);
    [SerializeField] private LayerMask _targetLayer;
    [SerializeField, Min(1f)]
    private float _planarBroadphaseHalfDepth = 1000f;

    private const float PlanarEpsilon = 1e-6f;

    private readonly List<PlanarWeaponHit> _hits = new();
    private readonly List<Vector2> _projectedCorners = new(8);
    private readonly List<Vector2> _bladePolygon = new(8);
    private readonly List<Vector2> _clipA = new(12);
    private readonly List<Vector2> _clipB = new(12);

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
    }

    public IReadOnlyList<PlanarWeaponHit> ScanHits()
    {
        _hits.Clear();

        Transform hitbox = _hitboxCenter != null
            ? _hitboxCenter
            : transform;
        buildProjectedBladePolygon(hitbox);

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
        Vector3 queryCenter = new Vector3(
            hitbox.position.x,
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

    private void buildProjectedBladePolygon(Transform hitbox)
    {
        _projectedCorners.Clear();

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
                    _projectedCorners.Add(
                        toPlanar(hitbox.TransformPoint(localCorner))
                    );
                }
            }
        }

        _projectedCorners.Sort(comparePlanarPoints);
        removeDuplicateProjectedCorners(_projectedCorners);
        buildConvexHull(_projectedCorners, _bladePolygon);
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

        float targetPlaneX = target.attachedRigidbody != null
            ? target.attachedRigidbody.worldCenterOfMass.x
            : bounds.center.x;
        contactPoint = fromPlanar(planarContact, targetPlaneX);
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

    private static Vector2 toPlanar(Vector3 point)
    {
        return new Vector2(point.z, point.y);
    }

    private static Vector3 fromPlanar(Vector2 point, float planeX)
    {
        return new Vector3(planeX, point.y, point.x);
    }

    private static Vector3 getPlanarDirection(Vector3 direction)
    {
        direction.x = 0f;
        return direction.sqrMagnitude > PlanarEpsilon
            ? direction.normalized
            : Vector3.zero;
    }

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

        IReadOnlyList<PlanarWeaponHit> planarHits = ScanHits();
        float displayPlaneX = hitbox.position.x;

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
            Vector3 start = fromPlanar(polygon[i], planeX);
            Vector3 end = fromPlanar(
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
