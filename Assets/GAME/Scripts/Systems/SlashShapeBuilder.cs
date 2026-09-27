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
/// Along the band, u is distance travelled rather than a fraction of the whole,
/// so the texture stays where it was laid instead of rescaling under itself
/// every time the band grows. That is what makes it read as a brush stroke
/// being drawn rather than a picture being stretched.
/// </remarks>
public static class SlashShapeBuilder
{
    /// <summary>The blade as it stood one frame, in the effect's own frame.</summary>
    public struct Sample
    {
        public Vector3 Grip;
        public Vector3 Tip;
    }

    private static readonly List<Vector3> Vertices = new(1024);
    private static readonly List<Vector4> Uv0 = new(1024);
    private static readonly List<int> Triangles = new(3072);

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
        Triangles.Clear();

        int count = arc.Count;

        if (count < 2)
        {
            mesh.Clear();
            return 0f;
        }

        int subdivisions = Mathf.Max(1, profile.Smoothing);
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
            Sample p1 = arc[i];
            Sample p2 = arc[i + 1];
            Sample p0 = arc[Mathf.Max(0, i - 1)];
            Sample p3 = arc[Mathf.Min(count - 1, i + 2)];

            for (int k = 0; k < subdivisions; k++)
            {
                float t = k / (float)subdivisions;

                Vector3 grip = catmullRom(p0.Grip, p1.Grip, p2.Grip, p3.Grip, t);
                Vector3 tip = catmullRom(p0.Tip, p1.Tip, p2.Tip, p3.Tip, t);

                addRib(grip, tip, inner, outer, span, ref travelled, ref previous, ref started);
            }
        }

        Sample last = arc[count - 1];
        addRib(last.Grip, last.Tip, inner, outer, span, ref travelled, ref previous, ref started);

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
        mesh.SetTriangles(Triangles, 0, true);

        return travelled / span;
    }

    /// <summary>
    /// Lays one cross-section of the band: a pair of vertices across the blade,
    /// carrying how far along the cut they sit.
    /// </summary>
    private static void addRib(
        Vector3 grip,
        Vector3 tip,
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
    }

    private static Vector3 catmullRom(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
    {
        float t2 = t * t;
        float t3 = t2 * t;
        return 0.5f * (
            2f * p1 +
            (-p0 + p2) * t +
            (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 +
            (-p0 + 3f * p1 - 3f * p2 + p3) * t3);
    }
}
