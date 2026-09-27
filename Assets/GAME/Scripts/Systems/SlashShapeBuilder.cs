using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Builds the slash's own shape as a band swept along the blade's path.
/// </summary>
/// <remarks>
/// The shape used to be a ring mesh that came with the effect, put into the
/// world once and then uncovered by a shader as the swing went on. That can
/// only ever approximate the cut: the ring is a perfect circle and a swing is
/// not, so the drawn arc and the blade agree at best at two points and drift
/// everywhere between. Worse, nothing about it is the swing - the radius is a
/// guess from the blade's length and how fast it uncovers is a guess at how far
/// the animation turns.
///
/// Built here instead, the band IS the path. Its leading edge is the blade by
/// construction, exactly as the ribbon's is, so there is nothing left to keep
/// in step: the shape sweeps because the geometry only exists where the weapon
/// has already been.
///
/// Between two frames the blade is filled in as a turn, not as a straight
/// line. A fast swing turns tens of degrees a frame; joining the tips across
/// that as points - even with a spline - cuts the chord of every step, and
/// the outline comes out as a polygon with a corner on every frame. So the
/// grip, the blade's direction and its length are interpolated separately and
/// the tip is rebuilt from them: the fill runs round the arc the blade
/// actually turned through. The spline is centripetal, which does not
/// overshoot where the frames are unevenly spaced - a swing that snaps from
/// slow to fast - and each step is cut finely enough that no facet turns more
/// than the profile's Arc Segment Degrees.
///
/// What the animation itself does is another matter: a keyframed arm moves in
/// straight runs between its keys, so the blade's own path has corners at the
/// clip's frame rate. Arc Smoothing irons those out, averaging each sample's
/// grip, direction and length towards its neighbours. The two ends are held -
/// the head exactly on the blade, the tail where the swing began - so the
/// stroke still leaves the weapon and still starts where it started.
///
/// Along the band, u is distance travelled rather than a fraction of the whole,
/// so the texture stays where it was laid instead of rescaling under itself
/// every time the band grows. That is what makes it read as a brush stroke
/// being drawn rather than a picture being stretched.
///
/// A second channel carries whether the attack could hit when each part of the
/// band was laid. The damage window is a stretch of the swing, not all of it,
/// and only the builder knows which vertices came from which frame - so it is
/// written here, per rib, and a shader that wants to thin the wind-up and
/// follow-through down to a boundary line reads it from uv1.
/// </remarks>
public static class SlashShapeBuilder
{
    /// <summary>The blade as it stood one frame, in the effect's own frame.</summary>
    public struct Sample
    {
        public Vector3 Grip;
        public Vector3 Tip;

        /// <summary>
        /// Whether the damage window was open when this blade was laid, 0 to
        /// 1. Recorded on the sample rather than looked up later, because the
        /// window moves on and the band keeps every sample from the swing.
        /// </summary>
        public float Hot;
    }

    /// <summary>
    /// The most segments one frame of swing is ever cut into, however far the
    /// blade turned, so a hitch that skips half the swing in one frame cannot
    /// build a mesh of thousands of ribs.
    /// </summary>
    private const int MaxSegmentsPerStep = 48;

    /// <summary>Centripetal parameterisation: the square root of the distance between frames.</summary>
    private const float Alpha = 0.5f;

    private static readonly List<Vector3> Vertices = new(1024);
    private static readonly List<Vector4> Uv0 = new(1024);
    private static readonly List<Vector2> Uv1 = new(1024);
    private static readonly List<int> Triangles = new(3072);

    private static readonly List<Vector3> Grips = new(64);
    private static readonly List<Vector3> Directions = new(64);
    private static readonly List<float> Lengths = new(64);
    private static readonly List<Vector3> ScratchGrips = new(64);
    private static readonly List<Vector3> ScratchDirections = new(64);
    private static readonly List<float> ScratchLengths = new(64);

    /// <returns>
    /// The u the band's head sits at: how far along the texture the blade has
    /// got. The band's own u only says how far a point is from the start, so a
    /// shader that shades by position along the stroke - thick at the blade,
    /// thin at the tail - needs this to know where the stroke currently ends.
    /// </returns>
    public static float Build(SlashProfile profile, List<Sample> arc, Mesh mesh)
    {
        Vertices.Clear();
        Uv0.Clear();
        Uv1.Clear();
        Triangles.Clear();

        int count = arc.Count;

        if (count < 2)
        {
            mesh.Clear();
            return 0f;
        }

        decompose(arc);
        smooth(profile.ArcSmoothing);

        int minimum = Mathf.Max(1, profile.Smoothing);
        float degreesPerSegment = Mathf.Max(profile.ArcSegmentDegrees, 0.25f);
        float span = Mathf.Max(profile.ShapeArcLength, 1e-3f);
        float inner = profile.ShapeInnerAlongBlade;
        float outer = profile.ShapeOuterAlongBlade;

        float travelled = 0f;
        Vector3 previous = Vector3.zero;
        bool started = false;

        // Oldest to newest, so u climbs with the swing and the texture is laid
        // down in the order the blade laid it.
        for (int i = 0; i < count - 1; i++)
        {
            // As many cuts as the turn needs, never fewer than the profile's
            // floor: a slow stretch of swing keeps its old density, a fast one
            // gets as many as it takes to stay round.
            float turn = Vector3.Angle(Directions[i], Directions[i + 1]);
            int segments = Mathf.Clamp(
                Mathf.CeilToInt(turn / degreesPerSegment), minimum, MaxSegmentsPerStep);

            knots(i, out float t0, out float t1, out float t2, out float t3);

            for (int k = 0; k < segments; k++)
            {
                float s = k / (float)segments;
                float t = Mathf.Lerp(t1, t2, s);

                Vector3 grip = spline(Grips, i, t0, t1, t2, t3, t);
                Vector3 direction = spline(Directions, i, t0, t1, t2, t3, t);
                float length = spline(Lengths, i, t0, t1, t2, t3, t);

                if (direction.sqrMagnitude < 1e-10f)
                    direction = Directions[i];

                Vector3 tip = grip + direction.normalized * Mathf.Max(length, 0f);

                // Straight between the two samples rather than on the curve.
                // The window opens and closes on a frame, so there is no
                // shape to follow - a spline through that step would ring
                // either side of it and light a sliver outside the window.
                float hot = Mathf.Lerp(arc[i].Hot, arc[i + 1].Hot, s);

                addRib(grip, tip, hot, inner, outer, span, ref travelled, ref previous, ref started);
            }
        }

        int last = count - 1;
        addRib(Grips[last], Grips[last] + Directions[last] * Lengths[last], arc[last].Hot,
            inner, outer, span, ref travelled, ref previous, ref started);

        int ribs = Vertices.Count / 2;

        for (int i = 0; i < ribs - 1; i++)
        {
            int a = i * 2;

            Triangles.Add(a);
            Triangles.Add(a + 1);
            Triangles.Add(a + 2);

            Triangles.Add(a + 1);
            Triangles.Add(a + 3);
            Triangles.Add(a + 2);
        }

        mesh.Clear();
        mesh.SetVertices(Vertices);
        mesh.SetUVs(0, Uv0);
        mesh.SetUVs(1, Uv1);
        mesh.SetTriangles(Triangles, 0, true);

        return travelled / span;
    }

    /// <summary>
    /// Each sample as the three things the fill interpolates: where the grip
    /// was, which way the blade pointed, and how long it was.
    /// </summary>
    private static void decompose(List<Sample> arc)
    {
        Grips.Clear();
        Directions.Clear();
        Lengths.Clear();

        Vector3 last = Vector3.up;

        for (int i = 0; i < arc.Count; i++)
        {
            Vector3 blade = arc[i].Tip - arc[i].Grip;
            float length = blade.magnitude;

            // A blade of no length has no direction; it keeps the last one,
            // which is where it was pointing a frame ago.
            Vector3 direction = length > 1e-6f ? blade / length : last;
            last = direction;

            Grips.Add(arc[i].Grip);
            Directions.Add(direction);
            Lengths.Add(length);
        }
    }

    /// <summary>
    /// Averages every sample towards its neighbours, a few passes deep, with
    /// the ends held: the tail fully, the head fully and the frame before it
    /// by half, so the stroke's leading edge is still the blade.
    /// </summary>
    /// <remarks>
    /// Directions are averaged as directions - the mean of the neighbours,
    /// renormalised - so rounding a corner never pulls the arc inward the way
    /// averaging the tips would: the band keeps its radius and only loses
    /// its kinks.
    /// </remarks>
    private static void smooth(float amount)
    {
        int count = Grips.Count;
        amount = Mathf.Clamp01(amount);

        if (amount <= 0f || count < 3)
            return;

        int passes = 1 + Mathf.RoundToInt(amount * 4f);
        float weight = 0.5f * amount;

        for (int pass = 0; pass < passes; pass++)
        {
            // Every sample moves towards where its neighbours were before this
            // pass, not where the pass has already put them, so the result does
            // not depend on which end it was walked from.
            ScratchGrips.Clear();
            ScratchGrips.AddRange(Grips);
            ScratchDirections.Clear();
            ScratchDirections.AddRange(Directions);
            ScratchLengths.Clear();
            ScratchLengths.AddRange(Lengths);

            for (int i = 1; i < count - 1; i++)
            {
                // Full strength two frames back from the head and beyond.
                float w = weight * Mathf.Clamp01((count - 1 - i) / 2f);

                Grips[i] = Vector3.Lerp(
                    ScratchGrips[i], (ScratchGrips[i - 1] + ScratchGrips[i + 1]) * 0.5f, w);

                Vector3 mean = ScratchDirections[i - 1] + ScratchDirections[i + 1];
                if (mean.sqrMagnitude > 1e-10f)
                    Directions[i] = Vector3.Slerp(ScratchDirections[i], mean.normalized, w);

                Lengths[i] = Mathf.Lerp(
                    ScratchLengths[i], (ScratchLengths[i - 1] + ScratchLengths[i + 1]) * 0.5f, w);
            }
        }
    }

    /// <summary>
    /// Centripetal knots for the step from sample i to i + 1, spaced by the
    /// square root of how far the blade moved between frames.
    /// </summary>
    private static void knots(int i, out float t0, out float t1, out float t2, out float t3)
    {
        t0 = 0f;
        t1 = t0 + interval(i - 1, i);
        t2 = t1 + interval(i, i + 1);
        t3 = t2 + interval(i + 1, i + 2);
    }

    /// <summary>
    /// How far apart two samples are, by whichever end of the blade moved
    /// more. Usually that is the tip; but a blade can pivot on its point, and
    /// knots spaced by the tip alone would then be nearly coincident around a
    /// grip that did move, and throw it off to infinity.
    /// </summary>
    private static float interval(int a, int b)
    {
        float tip = (tipAt(b) - tipAt(a)).magnitude;
        float grip = (at(Grips, b) - at(Grips, a)).magnitude;

        return Mathf.Max(Mathf.Pow(Mathf.Max(tip, grip), Alpha), 1e-4f);
    }

    /// <summary>
    /// A sample's tip, with the ends extended by reflection so the first and
    /// last steps have a neighbour to curve towards instead of a duplicate.
    /// </summary>
    private static Vector3 tipAt(int i)
    {
        int count = Grips.Count;

        if (i < 0)
            return 2f * tip(0) - tip(1);
        if (i >= count)
            return 2f * tip(count - 1) - tip(count - 2);

        return tip(i);
    }

    private static Vector3 tip(int i) => Grips[i] + Directions[i] * Lengths[i];

    private static Vector3 at(List<Vector3> values, int i)
    {
        int count = values.Count;

        if (i < 0)
            return 2f * values[0] - values[1];
        if (i >= count)
            return 2f * values[count - 1] - values[count - 2];

        return values[i];
    }

    private static float at(List<float> values, int i)
    {
        int count = values.Count;

        if (i < 0)
            return 2f * values[0] - values[1];
        if (i >= count)
            return 2f * values[count - 1] - values[count - 2];

        return values[i];
    }

    /// <summary>
    /// Barry and Goldman's pyramid for a Catmull-Rom segment on arbitrary
    /// knots, evaluated between samples i and i + 1.
    /// </summary>
    private static Vector3 spline(List<Vector3> values, int i, float t0, float t1, float t2, float t3, float t)
    {
        Vector3 p0 = at(values, i - 1);
        Vector3 p1 = at(values, i);
        Vector3 p2 = at(values, i + 1);
        Vector3 p3 = at(values, i + 2);

        Vector3 a1 = (t1 - t) / (t1 - t0) * p0 + (t - t0) / (t1 - t0) * p1;
        Vector3 a2 = (t2 - t) / (t2 - t1) * p1 + (t - t1) / (t2 - t1) * p2;
        Vector3 a3 = (t3 - t) / (t3 - t2) * p2 + (t - t2) / (t3 - t2) * p3;
        Vector3 b1 = (t2 - t) / (t2 - t0) * a1 + (t - t0) / (t2 - t0) * a2;
        Vector3 b2 = (t3 - t) / (t3 - t1) * a2 + (t - t1) / (t3 - t1) * a3;

        return (t2 - t) / (t2 - t1) * b1 + (t - t1) / (t2 - t1) * b2;
    }

    private static float spline(List<float> values, int i, float t0, float t1, float t2, float t3, float t)
    {
        float p0 = at(values, i - 1);
        float p1 = at(values, i);
        float p2 = at(values, i + 1);
        float p3 = at(values, i + 2);

        float a1 = (t1 - t) / (t1 - t0) * p0 + (t - t0) / (t1 - t0) * p1;
        float a2 = (t2 - t) / (t2 - t1) * p1 + (t - t1) / (t2 - t1) * p2;
        float a3 = (t3 - t) / (t3 - t2) * p2 + (t - t2) / (t3 - t2) * p3;
        float b1 = (t2 - t) / (t2 - t0) * a1 + (t - t0) / (t2 - t0) * a2;
        float b2 = (t3 - t) / (t3 - t1) * a2 + (t - t1) / (t3 - t1) * a3;

        return (t2 - t) / (t2 - t1) * b1 + (t - t1) / (t2 - t1) * b2;
    }

    /// <summary>
    /// Lays one cross-section of the band: a pair of vertices across the blade,
    /// carrying how far along the cut they sit and whether the attack could
    /// hit when they were laid.
    /// </summary>
    private static void addRib(
        Vector3 grip,
        Vector3 tip,
        float hot,
        float inner,
        float outer,
        float span,
        ref float travelled,
        ref Vector3 previous,
        ref bool started)
    {
        Vector3 near = Vector3.LerpUnclamped(grip, tip, inner);
        Vector3 far = Vector3.LerpUnclamped(grip, tip, outer);
        Vector3 middle = (near + far) * 0.5f;

        // Distance along the band's own centre line, which is what the blade
        // actually drew - not the tip's path, which runs longer, nor the
        // grip's, which barely moves.
        if (started)
            travelled += Vector3.Distance(previous, middle);

        previous = middle;
        started = true;

        float u = travelled / span;

        Vertices.Add(near);
        Vertices.Add(far);

        // The third and fourth channels are the pack shader's texture scroll.
        // Left unwritten a mesh hands it a one in w, which would shift every
        // sample by a whole tile, so they are set here rather than left to the
        // vertex declaration's defaults.
        Uv0.Add(new Vector4(u, 0f, 0f, 0f));
        Uv0.Add(new Vector4(u, 1f, 0f, 0f));

        // Both vertices of a rib carry the same value: the window is a
        // moment in the swing, so it runs along the band rather than across.
        Uv1.Add(new Vector2(hot, 0f));
        Uv1.Add(new Vector2(hot, 0f));
    }
}
