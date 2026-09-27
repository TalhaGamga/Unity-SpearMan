using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Builds the slash's own shape as a ring struck about a fixed axis.
/// </summary>
/// <remarks>
/// The band builder next door is the honest one: its geometry only exists
/// where the weapon has already been, so it cannot come away from the blade.
/// This is the stylised alternative, and it makes a different promise. A
/// circle struck about one axis will not track a swing that wanders - so it
/// does not try to. The axis, the centre and the radius are read once, on the
/// frame the cue fires, and then held. Nothing about the ring moves after
/// that.
///
/// What moves is where the ring is bright. The blade's angle about that axis
/// is measured every frame and the stroke's head is put there, so the sweep
/// runs at exactly the speed of the swing while the circle stays a circle.
/// That is the whole trick, and it is why the old ring mesh failed where this
/// does not: that one tried to be the cut and drifted off the blade, while
/// this one only has to say where the blade is on a shape that was never
/// claiming to be the blade's path.
///
/// The ring does not have to be round. Squash, wobble and taper deform it off
/// the compass circle, because a true one reads as a UI element rather than as
/// something a sword did.
///
/// Everything here is in the effect's own frame, whose Z is the gameplay
/// plane's normal - so the ring lies in local XY and its axis is local Z.
/// </remarks>
public static class SlashCircleBuilder
{
    /// <summary>
    /// The circle, in the effect's own frame.
    /// </summary>
    /// <remarks>
    /// Everything but <see cref="Sweep"/> is captured on the stroke's first
    /// frame and then left alone; the sweep is the only thing the swing is
    /// allowed to move.
    /// </remarks>
    public struct Frame
    {
        /// <summary>Centre of the ring - the grip, where the swing pivots.</summary>
        public Vector3 Center;

        /// <summary>Normal of the plane the ring lies in. Unit length.</summary>
        public Vector3 Axis;

        /// <summary>
        /// Where the blade pointed at the start, so angle zero. Unit length,
        /// and in the ring's plane.
        /// </summary>
        public Vector3 Reference;

        /// <summary>
        /// The widest circle the blade sweeps: the furthest the tip has got
        /// from <see cref="Center"/>, in the ring's own plane.
        /// </summary>
        /// <remarks>
        /// Unlike the rest of the frame this one does keep growing, because
        /// the blade's length is only where the tip started. The arm extends
        /// through the swing and the hand travels, so by the end the tip is
        /// reaching well past a ring struck at the blade's length - which put
        /// the cut inside the weapon that made it. Seeded from the blade so it
        /// never starts at nothing, and never allowed to shrink, so the ring
        /// settles on the swing's widest point rather than breathing with it.
        /// </remarks>
        public float Radius;

        /// <summary>
        /// Radians the blade has turned about the axis since then, signed and
        /// accumulated rather than wrapped.
        /// </summary>
        public float Sweep;

        /// <summary>
        /// Which way round the swing went, plus or minus one.
        /// </summary>
        /// <remarks>
        /// Latched by the stroke once the blade has actually turned, rather
        /// than read off the sweep here. On the first frame the sweep is still
        /// zero, so reading it would call every swing positive and then flip
        /// the moment a left-handed one registered - which mirrors the texture
        /// for one frame at the very start of half the attacks in the game.
        /// </remarks>
        public float Winding;
    }

    private static readonly List<Vector3> Vertices = new(544);
    private static readonly List<Vector4> Uv0 = new(544);
    private static readonly List<Color> Colors = new(544);
    private static readonly List<int> Triangles = new(1536);

    public static void Build(SlashProfile profile, Frame frame, Mesh mesh)
    {
        Vertices.Clear();
        Uv0.Clear();
        Colors.Clear();
        Triangles.Clear();

        if (frame.Radius <= 1e-4f || frame.Axis.sqrMagnitude < 1e-8f)
        {
            mesh.Clear();
            return;
        }

        int segments = Mathf.Clamp(profile.CircleSegments, 12, 256);
        float span = Mathf.Max(profile.CircleSpanDegrees, 1f) * Mathf.Deg2Rad;

        // Which way round the swing went, so a stroke drawn behind the blade
        // trails the swing rather than running ahead of it.
        float winding = frame.Winding < 0f ? -1f : 1f;

        // The head is where the blade is now. Held at the start angle when the
        // ring is not meant to follow, which strikes the shape in place
        // instead of sweeping it.
        float head = profile.CircleFollowSweep ? frame.Sweep : 0f;

        Vector3 axis = frame.Axis.normalized;
        Vector3 right = frame.Reference;
        Vector3 up = Vector3.Cross(axis, right);

        // Off the plane the weapon itself is swinging in, or the ring would be
        // sliced in half by the blade and the character it is drawn around -
        // both of them solid, and both of them sitting right on it.
        Vector3 centre = frame.Center + axis * profile.CircleDepthOffset;

        float inner = frame.Radius * profile.ShapeInnerAlongBlade;
        float outer = frame.Radius * profile.ShapeOuterAlongBlade;

        float squash = Mathf.Clamp(profile.CircleSquash, 0.05f, 1f);
        float wobble = profile.CircleWobble;
        int lobes = Mathf.Max(1, profile.CircleWobbleLobes);
        float taper = Mathf.Clamp01(profile.CircleTaper);
        float falloff = Mathf.Max(0f, profile.CircleHeadFalloff);
        float tail = Mathf.Clamp01(profile.CircleTailOpacity);

        int frames = Mathf.Max(1, profile.CircleTextureFrames);
        int cell = Mathf.Clamp(profile.CircleTextureFrame, 0, frames - 1);

        float u0 = cell / (float)frames;
        float u1 = (cell + 1) / (float)frames;

        if (profile.CircleTextureFlip)
            (u0, u1) = (u1, u0);

        for (int i = 0; i <= segments; i++)
        {
            // Zero at the stroke's tail, one at its head. Every falloff below
            // is written from the blade backwards, which is the way the stroke
            // is read.
            float t = i / (float)segments;
            float angle = head - (1f - t) * span * winding;

            // A real ellipse rather than a scaled circle: the direction bends
            // with it, which is what stops a squashed ring reading as a round
            // one that has merely been shrunk.
            Vector3 point = right * Mathf.Cos(angle) + up * (Mathf.Sin(angle) * squash);
            float scale = point.magnitude;

            if (scale < 1e-5f)
                continue;

            Vector3 direction = point / scale;
            scale *= 1f + wobble * Mathf.Sin(lobes * angle);

            float far = outer * scale;
            float near = inner * scale;

            // The band closes onto its outer edge as it falls behind, so the
            // stroke runs out to a point instead of stopping at a squared-off
            // end that nothing in a swing would produce.
            near = Mathf.Lerp(far, near, Mathf.Lerp(1f - taper, 1f, t));

            Vertices.Add(centre + direction * near);
            Vertices.Add(centre + direction * far);

            float u = Mathf.Lerp(u0, u1, t);

            // The third and fourth channels are the pack shader's texture
            // scroll. Left unwritten a mesh hands it a one in w, which would
            // shift every sample by a whole tile, so they are set here rather
            // than left to the vertex declaration's defaults.
            Uv0.Add(new Vector4(u, 0f, 0f, 0f));
            Uv0.Add(new Vector4(u, 1f, 0f, 0f));

            // Brightness is the sweep: the geometry is a whole circle from the
            // first frame, so it is where the ring is brightest that says how
            // far the swing has got.
            //
            // The tail is a floor under that rather than zero, because a ring
            // whose far side fades out entirely is not read as a circle at
            // all - it is read as an open arc chasing the blade, however
            // complete the geometry underneath it happens to be.
            float lead = falloff > 0f ? Mathf.Pow(t, falloff) : 1f;
            var tint = new Color(1f, 1f, 1f, Mathf.Lerp(tail, 1f, lead));

            Colors.Add(tint);
            Colors.Add(tint);
        }

        int ribs = Vertices.Count / 2;

        if (ribs < 2)
        {
            mesh.Clear();
            return;
        }

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
        mesh.SetColors(Colors);
        mesh.SetTriangles(Triangles, 0, true);
    }
}
