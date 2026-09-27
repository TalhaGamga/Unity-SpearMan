using System.Collections.Generic;
using UnityEngine;

namespace Combat
{
    /// <summary>
    /// Turns the path a blade has swept into one mesh.
    ///
    /// The input is a spine: the blade segment as it was on each of the last few
    /// frames. The output is the surface those segments sweep, dressed with a
    /// highlight, accent streaks and volume shells.
    ///
    /// The surface is built in three dimensions, on the axis the blade actually
    /// travelled. An earlier version flattened every sample onto the gameplay
    /// plane first, which cost two things at once: the stroke followed the
    /// projection of the swing rather than the swing, and being a plane it could
    /// not have any thickness to light or to offset. A swing is rarely flat even
    /// in a game whose traversal is, and the twist it has is most of what reads
    /// as weight.
    ///
    /// Each rib therefore carries its own frame: along the blade, along the
    /// trail, and the surface normal between them. Layers and shells are offset
    /// along that normal, so they stack through the surface instead of through
    /// an assumed camera axis.
    ///
    /// Everything lands in the same vertex buffer and the same submesh. Which
    /// part a triangle belongs to travels in its vertex data, so the whole
    /// effect draws once.
    /// </summary>
    public static class SlashRibbonBuilder
    {
        /// <summary>The blade as it was at one instant.</summary>
        public struct Sample
        {
            /// <summary>Point on the blade where the stroke's inner edge runs.</summary>
            public Vector3 Inner;
            /// <summary>The blade's tip.</summary>
            public Vector3 Outer;
            public float Time;
        }

        private static readonly List<Vector3> Vertices = new(2048);
        private static readonly List<Vector4> Uv0 = new(2048);
        private static readonly List<Vector4> Uv1 = new(2048);
        private static readonly List<Color> Colors = new(2048);
        private static readonly List<int> Triangles = new(6144);

        /// <summary>One point of the smoothed ribbon, with the frame it sits in.</summary>
        private struct Rib
        {
            /// <summary>Age along the trail: 0 on the blade, 1 at the oldest end.</summary>
            public float U;
            /// <summary>Inner edge after narrowing.</summary>
            public Vector3 Inner;
            /// <summary>Outer edge, always the tip's own path.</summary>
            public Vector3 Outer;
            /// <summary>Unit vector from inner to outer, along the blade.</summary>
            public Vector3 Out;
            /// <summary>Unit vector along the trail, from older toward the blade.</summary>
            public Vector3 Along;
            /// <summary>Surface normal, for stacking layers through the sweep.</summary>
            public Vector3 Normal;
            /// <summary>Full width of the blade segment here, before narrowing.</summary>
            public float Width;
        }

        private static readonly List<Rib> Ribs = new(512);

        /// <summary>
        /// Rebuilds <paramref name="mesh"/> from the swept path in
        /// <paramref name="spine"/>, newest entry last.
        /// </summary>
        public static void Build(
            SlashProfile profile, List<Sample> spine, float now, Mesh mesh)
        {
            if (profile == null || mesh == null)
                return;

            Vertices.Clear();
            Uv0.Clear();
            Uv1.Clear();
            Colors.Clear();
            Triangles.Clear();

            buildRibs(profile, spine, now);

            if (Ribs.Count < 2)
            {
                mesh.Clear();
                return;
            }

            appendVolume(profile);
            appendBody(profile);
            appendHighlight(profile);
            appendAccents(profile);

            mesh.Clear();

            if (Vertices.Count == 0)
                return;

            mesh.SetVertices(Vertices);
            mesh.SetUVs(0, Uv0);
            mesh.SetUVs(1, Uv1);
            mesh.SetColors(Colors);
            mesh.SetTriangles(Triangles, 0, false);
            mesh.RecalculateBounds();
        }

        /// <summary>
        /// Smooths the spine and works out, once, everything the layers need:
        /// where each edge runs, the frame it sits in, and how far back along
        /// the trail the point is.
        /// </summary>
        private static void buildRibs(
            SlashProfile profile, List<Sample> spine, float now)
        {
            Ribs.Clear();

            int count = spine.Count;
            if (count < 2)
                return;

            int subdivisions = Mathf.Max(1, profile.Smoothing);
            float trail = Mathf.Max(profile.TrailSeconds, 1e-3f);

            // Walk newest to oldest so u climbs with the index, which keeps the
            // triangle winding and the shader's sense of "behind" in agreement.
            for (int i = count - 1; i > 0; i--)
            {
                Sample p1 = spine[i];
                Sample p2 = spine[i - 1];
                Sample p0 = spine[Mathf.Min(count - 1, i + 1)];
                Sample p3 = spine[Mathf.Max(0, i - 2)];

                for (int k = 0; k < subdivisions; k++)
                {
                    float t = k / (float)subdivisions;

                    addRib(
                        profile,
                        catmullRom(p0.Inner, p1.Inner, p2.Inner, p3.Inner, t),
                        catmullRom(p0.Outer, p1.Outer, p2.Outer, p3.Outer, t),
                        now - Mathf.Lerp(p1.Time, p2.Time, t),
                        trail);
                }
            }

            Sample last = spine[0];
            addRib(profile, last.Inner, last.Outer, now - last.Time, trail);

            resolveFrames();
        }

        private static void addRib(
            SlashProfile profile, Vector3 inner, Vector3 outer, float age, float trail)
        {
            float u = Mathf.Clamp01(age / trail);

            Vector3 span = outer - inner;
            float width = span.magnitude;

            // The stroke narrows onto the tip's path as it falls behind, so the
            // oldest end closes to a line instead of stopping on a flat edge.
            float keep = Mathf.Pow(1f - u, Mathf.Max(profile.TaperSharpness, 0.05f));

            Ribs.Add(new Rib
            {
                U = u,
                Inner = Vector3.Lerp(outer, inner, keep),
                Outer = outer,
                Out = width > 1e-5f ? span / width : Vector3.up,
                Width = width
            });
        }

        /// <summary>
        /// Gives every rib its trail direction and surface normal.
        ///
        /// The trail direction is a central difference along the tip's path, so
        /// it stays smooth where a one-sided difference would jump at the ends.
        /// The normal is the cross of the two, which is the sweep's own normal -
        /// the direction a swing has thickness in.
        /// </summary>
        private static void resolveFrames()
        {
            int count = Ribs.Count;
            Vector3 carried = Vector3.right;

            for (int i = 0; i < count; i++)
            {
                Rib rib = Ribs[i];

                Vector3 ahead = Ribs[Mathf.Max(i - 1, 0)].Outer;
                Vector3 behind = Ribs[Mathf.Min(i + 1, count - 1)].Outer;
                Vector3 along = ahead - behind;

                rib.Along = along.sqrMagnitude > 1e-10f ? along.normalized : carried;

                Vector3 normal = Vector3.Cross(rib.Out, rib.Along);

                // A blade travelling straight along its own length has no
                // sweep to take a normal from; carrying the last good one keeps
                // the surface from flipping inside out for a frame.
                rib.Normal = normal.sqrMagnitude > 1e-10f
                    ? normal.normalized
                    : carried;

                carried = rib.Normal;
                Ribs[i] = rib;
            }
        }

        /// <summary>
        /// Shells of the body stacked through the sweep's own normal.
        ///
        /// This is where the stroke gets its bulk. A single sheet has no
        /// thickness from any angle; a few shells spread across the normal read
        /// as a solid mass, and because they are offset in three dimensions
        /// they slide against each other as the camera or the character moves,
        /// which is the parallax that sells it.
        /// </summary>
        private static void appendVolume(SlashProfile profile)
        {
            int shells = profile.VolumeShells;

            if (shells <= 0 || profile.VolumeOpacity <= 0.001f)
                return;

            for (int i = 0; i < shells; i++)
            {
                // Paired either side of the body, so the mass grows around the
                // sweep rather than drifting off one face of it.
                float side = (i % 2 == 0) ? 1f : -1f;
                int step = i / 2 + 1;
                float depth = side * step * profile.VolumeSeparation;
                float fade = 1f - (step - 1) / (float)Mathf.Max(1, (shells + 1) / 2);

                appendBand(
                    profile,
                    from: 0f, to: 1f,
                    innerFrac: 1f + profile.VolumeSpread,
                    outerFrac: -profile.VolumeSpread,
                    shapeBias: 0f, shapeSharpness: 0f,
                    normalOffset: profile.DepthOffset + depth,
                    opacity: profile.VolumeOpacity * Mathf.Max(fade, 0.15f),
                    coreWidth: 0.25f,
                    falloff: Mathf.Max(profile.EdgeFalloff * 0.55f, 0.5f),
                    outside: false);
            }
        }

        private static void appendBody(SlashProfile profile)
        {
            appendBand(
                profile,
                from: 0f, to: 1f,
                innerFrac: 1f, outerFrac: 0f,
                shapeBias: 0f, shapeSharpness: 0f,
                normalOffset: profile.DepthOffset,
                opacity: 1f,
                coreWidth: 0.55f,
                falloff: profile.EdgeFalloff,
                outside: false);
        }

        private static void appendHighlight(SlashProfile profile)
        {
            if (profile.HighlightOpacity <= 0.001f)
                return;

            SlashProfile.HighlightSpec spec = profile.ResolveHighlight();
            float length = Mathf.Min(spec.Length, 1f - spec.Start);

            if (length <= 0.01f || spec.Width <= 0.001f)
                return;

            appendBand(
                profile,
                from: spec.Start, to: spec.Start + length,
                innerFrac: spec.Offset + spec.Width, outerFrac: spec.Offset,
                shapeBias: spec.Bias, shapeSharpness: spec.Sharpness,
                normalOffset: profile.DepthOffset + profile.LayerSeparation,
                opacity: profile.HighlightOpacity,
                coreWidth: 0.85f,
                falloff: profile.HighlightFalloff,
                outside: false);
        }

        private static void appendAccents(SlashProfile profile)
        {
            if (profile.AccentCount <= 0 || profile.AccentOpacity <= 0.001f)
                return;

            for (int i = 0; i < profile.AccentCount; i++)
            {
                // Alternate sides and step outward by lane, so two accents sit
                // either side of the tip's path and four bracket it evenly.
                // Nothing here is random: the same profile always produces the
                // same streaks.
                float side = (i % 2 == 0) ? 1f : -1f;
                int lane = i / 2;

                float gap = profile.AccentGap + lane * profile.AccentSpacing;
                float near = side > 0f ? gap : -gap - profile.AccentWidth;

                float start = Mathf.Clamp01(profile.AccentStart + 0.06f * i);
                float length = Mathf.Min(
                    profile.AccentLength * (1f - 0.14f * i),
                    1f - start);

                if (length <= 0.02f)
                    continue;

                appendBand(
                    profile,
                    from: start, to: start + length,
                    innerFrac: near, outerFrac: near + profile.AccentWidth,
                    shapeBias: 0.35f + 0.06f * i, shapeSharpness: 2f,
                    normalOffset: profile.DepthOffset +
                        profile.LayerSeparation * (side > 0f ? 2f : -1f),
                    opacity: profile.AccentOpacity * (1f - 0.12f * i),
                    coreWidth: 0.9f,
                    falloff: profile.HighlightFalloff,
                    outside: true);
            }
        }

        /// <summary>
        /// Lays one band over the part of the trail between
        /// <paramref name="from"/> and <paramref name="to"/>.
        ///
        /// Its two edges are given as fractions of the stroke's width, measured
        /// in from the tip's path. A band inside the ribbon measures against
        /// the narrowed inner edge, so it shrinks with the body; one outside it
        /// measures against the blade's own width, so a motion line keeps its
        /// distance instead of collapsing onto the stroke as that closes.
        ///
        /// <paramref name="normalOffset"/> moves the whole band along each
        /// rib's own surface normal, which is what stacks the layers through
        /// the sweep rather than through a fixed axis.
        /// </summary>
        private static void appendBand(
            SlashProfile profile, float from, float to, float innerFrac, float outerFrac,
            float shapeBias, float shapeSharpness, float normalOffset, float opacity,
            float coreWidth, float falloff, bool outside)
        {
            int baseIndex = Vertices.Count;
            var color = new Color(1f, 1f, 1f, opacity);
            float span = Mathf.Max(to - from, 1e-4f);
            int written = 0;

            for (int i = 0; i < Ribs.Count; i++)
            {
                Rib rib = Ribs[i];

                if (rib.U < from || rib.U > to)
                    continue;

                float shape = shapeSharpness > 0f
                    ? taper((rib.U - from) / span, shapeBias, shapeSharpness)
                    : 1f;

                Vector3 a, b;

                if (outside)
                {
                    a = rib.Outer + rib.Out * (innerFrac * rib.Width * shape);
                    b = rib.Outer + rib.Out * (outerFrac * rib.Width * shape);
                }
                else
                {
                    a = Vector3.LerpUnclamped(rib.Outer, rib.Inner, innerFrac * shape);
                    b = Vector3.LerpUnclamped(rib.Outer, rib.Inner, outerFrac * shape);
                }

                Vector3 offset = rib.Normal * normalOffset;

                Vertices.Add(a + offset);
                Vertices.Add(b + offset);
                Uv0.Add(new Vector4(rib.U, 0f, coreWidth, falloff));
                Uv0.Add(new Vector4(rib.U, 1f, coreWidth, falloff));
                Uv1.Add(new Vector4(0f, 0f, rib.U, 0f));
                Uv1.Add(new Vector4(0f, 0f, rib.U, 0f));
                Colors.Add(color);
                Colors.Add(color);
                written++;
            }

            for (int i = 0; i < written - 1; i++)
            {
                int a = baseIndex + i * 2;
                Triangles.Add(a);
                Triangles.Add(a + 1);
                Triangles.Add(a + 2);
                Triangles.Add(a + 1);
                Triangles.Add(a + 3);
                Triangles.Add(a + 2);
            }
        }


        /// <summary>
        /// One smooth hump that reaches zero at both ends, with a movable peak.
        ///
        /// It is the normalised curve <c>t^a (1-t)^b</c>, whose peak sits at
        /// <c>a / (a + b)</c>. Solving that for the wanted bias gives the two
        /// exponents. Being a single analytic expression it has no joins, so a
        /// streak cannot crease where two pieces of a hand-made curve would
        /// meet.
        /// </summary>
        private static float taper(float t, float bias, float sharpness)
        {
            t = Mathf.Clamp01(t);
            bias = Mathf.Clamp(bias, 0.05f, 0.95f);
            sharpness = Mathf.Max(0.05f, sharpness);

            float a = sharpness * bias * 2f;
            float b = sharpness * (1f - bias) * 2f;

            float peak = Mathf.Pow(bias, a) * Mathf.Pow(1f - bias, b);
            if (peak < 1e-6f)
                return 0f;

            return Mathf.Pow(t, a) * Mathf.Pow(1f - t, b) / peak;
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
}
