using UnityEngine;

/// <summary>
/// Every dial of one slash effect, in one asset.
///
/// The effect is the surface the blade actually sweeps. Each frame the visible
/// blade is a segment, and the trail is the ribbon those segments leave behind;
/// nothing here describes a shape the weapon might have cut, only how the
/// shape it did cut is dressed.
///
/// That matters because the obvious alternative - a circular arc struck around
/// the grip - is wrong in three ways at once on an animated character. The grip
/// travels its own arc as the arm swings, the blade tilts out of the camera's
/// plane so its apparent length changes, and the character can move underneath
/// the whole thing. An arc drawn from an angle and a radius cannot follow any
/// of that, which is why it comes away from the weapon.
///
/// Placement - which point on the weapon is the grip, which is the tip - stays
/// on the cue entry, because that is about the weapon. This asset is only about
/// the look, so one profile can be shared by every weapon that should slash the
/// same way.
/// </summary>
[CreateAssetMenu(
    menuName = "ScriptableObjects/Combat/Slash Profile",
    fileName = "SlashProfile")]
public class SlashProfile : ScriptableObject
{
    [Header("Ribbon")]
    [Tooltip("Draw the generated ribbon at all. Turn it off to let an authored " +
        "particle effect be the whole stroke; everything else about the trail - " +
        "when it starts, how long it lays down, where the blade is - keeps " +
        "working, because the particles are driven by the same path.")]
    public bool DrawRibbon = true;

    [Tooltip("Where on the blade the trail's inner edge runs. 0 is the grip " +
        "and 1 the tip, so a lower value gives a broader stroke. This is the " +
        "stroke's width: it is a piece of the blade, which is why the trail " +
        "meets the weapon instead of floating near it.")]
    [Range(0f, 0.95f)] public float StartAlongBlade = 0.3f;

    [Tooltip("How long a piece of trail lives, in seconds. This is the " +
        "stroke's length: a faster swing lays down more of it in the same " +
        "time, exactly as a real trail would.")]
    [Range(0.02f, 0.6f)] public float TrailSeconds = 0.14f;

    [Tooltip("How quickly the stroke narrows onto the tip's path as it falls " +
        "behind. Higher is a thinner, sharper trail; lower keeps the body " +
        "broad for longer.")]
    [Range(0.3f, 4f)] public float TaperSharpness = 1.2f;

    [Tooltip("Tip travel in world units before the trail lays down another " +
        "vertebra. Lower is smoother and heavier.")]
    [Range(0.005f, 0.2f)] public float MinStep = 0.02f;

    [Tooltip("Curve subdivisions between vertebrae. This is what takes the " +
        "frame-rate steps out of the stroke's edges.")]
    [Range(1, 8)] public int Smoothing = 4;

    [Header("Highlight")]
    [Tooltip("The bright inner streak's character. Anything but Custom fills " +
        "in the five numbers below.")]
    public SlashHighlightShape HighlightShape = SlashHighlightShape.Sword;

    [Tooltip("Custom only. Width as a fraction of the stroke's own width.")]
    [Range(0.05f, 1f)] public float HighlightWidth = 0.45f;

    [Tooltip("Custom only. Where the streak begins, as a fraction of the " +
        "trail's length back from the blade.")]
    [Range(0f, 0.6f)] public float HighlightStart = 0.05f;

    [Tooltip("Custom only. How much of the trail the streak covers.")]
    [Range(0.1f, 1f)] public float HighlightLength = 0.8f;

    [Tooltip("Custom only. Where the streak swells. Late means a stroke that " +
        "widens as it falls behind; early means one that leads with its mass.")]
    [Range(0.05f, 0.95f)] public float HighlightBias = 0.4f;

    [Tooltip("Custom only. How pointed the streak's ends are.")]
    [Range(0.3f, 4f)] public float HighlightSharpness = 1.4f;

    [Tooltip("Custom only. How far in from the tip's edge the streak sits, as " +
        "a fraction of the stroke's width. Zero hugs the tip.")]
    [Range(0f, 0.8f)] public float HighlightOffset = 0.05f;

    [Tooltip("Opacity of the streak over the body.")]
    [Range(0f, 1f)] public float HighlightOpacity = 0.95f;

    [Header("Accent Streaks")]
    [Tooltip("Thin motion lines running outside the tip's path. They follow " +
        "the same curve, so they read as speed rather than as extra shapes.")]
    [Range(0, 4)] public int AccentCount = 2;

    [Tooltip("Thickness of each accent, as a fraction of the stroke's width.")]
    [Range(0.01f, 0.4f)] public float AccentWidth = 0.09f;

    [Tooltip("Clearance between the tip's path and the first accent, as a " +
        "fraction of the stroke's width.")]
    [Range(0f, 1.5f)] public float AccentGap = 0.25f;

    [Tooltip("Extra clearance for each accent past the first.")]
    [Range(0.05f, 1f)] public float AccentSpacing = 0.22f;

    [Tooltip("Where the first accent begins along the trail. Each following " +
        "one starts slightly further back.")]
    [Range(0f, 0.7f)] public float AccentStart = 0.05f;

    [Tooltip("How much of the trail the first accent covers. Each following " +
        "one is slightly shorter.")]
    [Range(0.1f, 1f)] public float AccentLength = 0.7f;

    [Tooltip("Opacity of the accents.")]
    [Range(0f, 1f)] public float AccentOpacity = 0.7f;

    [Header("Volume")]
    [Tooltip("Copies of the body stacked either side of the sweep, through its " +
        "own normal. This is where the stroke gets its bulk: one sheet has no " +
        "thickness from any angle, while a few shells spread across the normal " +
        "read as mass and slide against each other as the view moves. Zero " +
        "leaves the stroke flat.")]
    [Range(0, 6)] public int VolumeShells = 4;

    [Tooltip("Distance between shells, in world units. With the shell count, " +
        "this is how thick the stroke is.")]
    [Range(0f, 0.3f)] public float VolumeSeparation = 0.035f;

    [Tooltip("How much wider each shell is than the body, as a fraction of its " +
        "width. A little swell stops the shells reading as a stack of identical " +
        "sheets.")]
    [Range(0f, 0.6f)] public float VolumeSpread = 0.18f;

    [Tooltip("Opacity of the innermost shell; the outer ones fall off from it. " +
        "Low is right - shells exist to be seen through.")]
    [Range(0f, 1f)] public float VolumeOpacity = 0.4f;

    [Header("Shape")]
    [Tooltip("How the cut itself is built - the slash's own shape, as opposed " +
        "to the ribbon trailing behind it.\n\n" +
        "Band sweeps it along the blade's real path, so it cannot come away " +
        "from the weapon however the swing wanders. Circle strikes a ring " +
        "about the axis the blade was on when the cue fired and lights it up " +
        "as the blade goes round: the whole circle is geometry from the first " +
        "frame, and the swing only says where it is brightest.\n\n" +
        "None draws no cut at all and leaves the ribbon as the whole stroke.")]
    public SlashShapeMode ShapeMode = SlashShapeMode.Band;

    [Header("Circle")]
    [Tooltip("Draws the ring. Left empty it falls back on the materials taken " +
        "from the particle prefab, the same ones the band is dressed with.")]
    public Material CircleMaterial;

    [Tooltip("How much of the ring exists, in degrees. 360 is the full circle; " +
        "less leaves a crescent whose leading edge still rides the blade.")]
    [Range(10f, 360f)] public float CircleSpanDegrees = 360f;

    [Tooltip("How big the ring is, measured over the stroke's own window - " +
        "from the start cue to the end of the swing - rather than taken from " +
        "the weapon.\n\n" +
        "Widest encloses the whole cut. Average is smaller and steadier, for " +
        "a swing that snaps out at the end and makes the widest reading jump.")]
    public SlashCircleRadius CircleRadiusSource = SlashCircleRadius.Widest;

    [Tooltip("Puts the stroke's bright head wherever the blade is, so the " +
        "sweep runs at the speed of the swing. Off holds it at the angle the " +
        "blade started on, which strikes the ring in place instead of " +
        "sweeping it.")]
    public bool CircleFollowSweep = true;

    [Tooltip("How quickly the ring dims behind the blade. This is what carries " +
        "the sweep: it is where the stroke is brightest that says how far the " +
        "swing has got, since the geometry is a whole circle from the first " +
        "frame. Zero lights it evenly all the way round.")]
    [Range(0f, 8f)] public float CircleHeadFalloff = 1.2f;

    [Tooltip("How bright the ring still is at its trailing end, against 1 at " +
        "the blade.\n\n" +
        "This is what keeps the circle a circle. Left at zero the tail fades " +
        "to nothing and the ring reads as an open arc chasing the blade, " +
        "whatever the geometry says - so raise it until the whole circle is " +
        "on screen, and use the falloff only to shape the bright part.")]
    [Range(0f, 1f)] public float CircleTailOpacity = 0.5f;

    [Tooltip("Flattens the ring across the swing, 1 being a true circle. " +
        "Below 1 it reads as a circle seen at an angle rather than face on.")]
    [Range(0.1f, 1f)] public float CircleSquash = 1f;

    [Tooltip("How far the rim wanders off the circle, as a fraction of its " +
        "radius. Zero is a true circle; a little wander trades that for " +
        "something more hand-drawn.")]
    [Range(0f, 0.5f)] public float CircleWobble;

    [Tooltip("How many bulges that wander puts around the ring.")]
    [Range(1, 8)] public int CircleWobbleLobes = 3;

    [Tooltip("How much the band closes onto its outer edge as it falls behind " +
        "the blade. Zero keeps an even ring; raising it runs the stroke out " +
        "to a point, which is a crescent rather than a circle.")]
    [Range(0f, 1f)] public float CircleTaper;

    [Tooltip("Sides around the ring. This is what takes the corners out of it.")]
    [Range(12, 256)] public int CircleSegments = 96;

    [Tooltip("Shift along the ring's own axis, towards the camera.\n\n" +
        "Unlike the trail's Depth Offset this is not a nicety: the ring is " +
        "struck in the plane the weapon swings in, so left at zero it is " +
        "sliced in half by the blade and by the character it is drawn around. " +
        "It wants to be clear of both.")]
    [Range(-0.5f, 0.5f)] public float CircleDepthOffset = 0.06f;

    [Tooltip("How many frames the ring's texture is divided into along its " +
        "width. The pack's swipe sheets are four side by side; a texture that " +
        "is one picture is one.")]
    [Range(1, 8)] public int CircleTextureFrames = 4;

    [Tooltip("Which of those frames to wrap around the ring.")]
    [Range(0, 7)] public int CircleTextureFrame = 0;

    [Tooltip("Wraps that frame the other way round, for a texture whose bright " +
        "end would otherwise land at the tail instead of on the blade.")]
    public bool CircleTextureFlip;

    [Header("Particles")]
    [Tooltip("Particles laid down along the blade's path. Every particle system " +
        "under this prefab is driven, and each one's own emission and shape are " +
        "switched off: the stroke knows where the blade went and the prefab does " +
        "not, so it places every particle itself. The prefab keeps only its look.")]
    // A prefab, not a single system: an authored effect is several systems
    // layered under one root, and the field has to be able to hold that root.
    public GameObject Particles;

    [Tooltip("Particles laid per world unit the tip travels, so the density of " +
        "the trail follows how hard the swing was rather than the frame rate.")]
    [Range(0f, 60f)] public float ParticlesPerUnit = 8f;

    [Tooltip("Fraction of the tip's own speed each particle carries away. Zero " +
        "leaves them hanging in the air the blade passed through, which is what " +
        "a trail is; anything much above zero drags the stroke across the scene " +
        "after the swing has finished.")]
    [Range(0f, 2f)] public float ParticleSpeedScale = 0f;

    [Tooltip("Random speed added in any direction, in units per second. Keep it " +
        "under the swing's own speed or the trail stops reading as one stroke.")]
    [Range(0f, 6f)] public float ParticleSpread = 0.4f;

    [Tooltip("Multiplies every laid particle's own start size, the slash's " +
        "shape included.")]
    [Range(0.1f, 6f)] public float ParticleSizeScale = 1f;

    [Tooltip("A system inside the prefab authored to fire this many particles " +
        "or fewer per shot is taken to be the slash's own shape rather than " +
        "spray around it, and is laid once per swing - in the plane the blade " +
        "swept, at the size it was drawn - instead of along the blade's path.\n\n" +
        "The distinction matters because the two are not the same kind of " +
        "thing. A system that fires a single particle is not a sparse spray: " +
        "it is one mesh drawn the width of the whole arc, with a dissolve " +
        "timed to its own lifetime, and it is the slash. Lay forty of those " +
        "along a swing and none of the forty is ever the shape - they overlap " +
        "into a band, each caught at a different moment of its own dissolve " +
        "and turned a different way, and the cut reads as a smear.\n\n" +
        "Raise it only if a shape layer is still being trailed; lower it to " +
        "zero to drive every system as spray.")]
    [Range(0, 20)] public int ShapeBurstCount = 4;

    [Tooltip("Where the band begins across the blade, as a fraction of its " +
        "length: 0 at the hand, 1 at the tip. The cut is a band around the " +
        "edge rather than the whole reach, so this is where its inside " +
        "runs.\n\n" +
        "In Circle mode this is the ring's inner radius - but as a fraction " +
        "of the circle the swing swept, not of the blade, so 1 sits right on " +
        "the cut.")]
    [Range(0f, 1f)] public float ShapeInnerAlongBlade = 0.55f;

    [Tooltip("Where the band ends, on the same scale. Above 1 it reaches " +
        "past the tip, which is usually right - a cut reads as wider than " +
        "the steel that made it.\n\n" +
        "In Circle mode this is the ring's outer radius, on the swept circle " +
        "rather than the blade.")]
    [Range(0.1f, 2f)] public float ShapeOuterAlongBlade = 1.15f;

    [Tooltip("How far the blade travels, in world units, to lay down one " +
        "full pass of the slash texture.\n\n" +
        "The band is measured by distance rather than divided into a " +
        "fraction of itself, so the texture stays where it was put instead " +
        "of rescaling under itself every time the band grows - that is what " +
        "makes it read as a stroke being drawn rather than a picture being " +
        "stretched. Set it near a swing's own arc length: shorter and the " +
        "pattern repeats along the cut, longer and only part of it is " +
        "reached.")]
    [Range(0.2f, 12f)] public float ShapeArcLength = 2.4f;

    [Tooltip("How long the cut takes to erode away once the swing is over, in " +
        "seconds. It dissolves rather than fading, through the same noise the " +
        "shape was drawn with.")]
    [Range(0.05f, 3f)] public float ShapeFadeSeconds = 0.35f;

    [Tooltip("Multiplies the spray's opacity. Only the spray: the slash's own " +
        "shape reads its alpha as a dissolve threshold rather than an opacity, " +
        "so dimming that would eat the shape away instead of fading it.")]
    [Range(0f, 1f)] public float ParticleOpacity = 1f;

    [Tooltip("How long one of the spray's particles lives, in seconds. Zero " +
        "keeps whatever each system was authored with, which for a one-shot " +
        "effect runs to seconds - long enough that a swing's debris is still " +
        "hanging in the air several swings later. A trail wants about its own " +
        "length. The slash's own shape always keeps its authored lifetime, " +
        "because its dissolve is timed against it.")]
    [Range(0f, 3f)] public float ParticleLifetime = 0.3f;

    [Tooltip("Turn each laid particle to face the way the blade swept - its " +
        "plane is the swing's plane, its up is the blade. Only systems drawn " +
        "as meshes are turned; billboards always face the camera and have no " +
        "orientation to set. Off leaves every particle in the mesh's own " +
        "default pose, which is where a slash ring lying flat comes from.")]
    public bool AlignParticlesToSweep = true;

    [Tooltip("Extra turn applied after that, in degrees. The correction for a " +
        "mesh whose flat pose is not the one this expects: if the rings come " +
        "out edge-on, ninety on one axis is usually it.")]
    public Vector3 ParticleRotationOffset;

    [Header("Colour")]
    [Tooltip("Draws every part of the stroke. Normally the shared Slash Arc " +
        "material; give a profile its own only to put it on a different shader.")]
    public Material Material;

    [ColorUsage(true, true)] public Color CoreColor = Color.white;

    [Tooltip("The colour that names the attack. Sits at the stroke's edges " +
        "where it frames the white core.")]
    [ColorUsage(true, true)] public Color EdgeColor = new Color(0.45f, 0.85f, 1f, 1f);

    [Tooltip("The soft skirt outside the body. Keep it dimmer than the edge " +
        "or the silhouette goes muddy.")]
    [ColorUsage(true, true)] public Color GlowColor = new Color(0.2f, 0.55f, 1f, 1f);

    [Tooltip("Overall output. Above about 2 this starts to drive bloom, which " +
        "is usually further than a stylised slash wants to go.")]
    [Range(0.2f, 4f)] public float Brightness = 1.6f;

    [Tooltip("How tight the white core is. Higher is a thinner, hotter line.")]
    [Range(0.5f, 8f)] public float CoreSharpness = 2.2f;

    [Tooltip("How much soft light spills past the stroke's body.")]
    [Range(0f, 2f)] public float GlowIntensity = 0.45f;

    [Tooltip("How hard the body's edges are. Higher is a crisper stroke.")]
    [Range(0.5f, 6f)] public float EdgeFalloff = 1.8f;

    [Tooltip("How hard the highlight's and accents' edges are.")]
    [Range(0.5f, 8f)] public float HighlightFalloff = 2.6f;

    [Header("Timing")]
    [Tooltip("A clip says when its stroke starts and stops, with VisualCueStart " +
        "and VisualCueEnd. Leave this on for clips that only author the start: " +
        "the stroke then also stops on its own once the blade stops moving. " +
        "Turn it off once the clips using this profile author both, so nothing " +
        "can cut a stroke short before its end cue arrives.")]
    public bool AutoStopWhenStill = true;

    [Tooltip("Longest the trail keeps laying itself down, whatever the clip " +
        "says. A backstop against a missing end cue, not a timing control: set " +
        "it above the longest swing that uses this profile.")]
    [Range(0.05f, 1.5f)] public float FollowSeconds = 0.45f;

    [Tooltip("Seconds the stroke takes to fade once the swing is over. It is " +
        "also shortening from the tail at the same time, so this only has to " +
        "cover what is left.")]
    [Range(0.02f, 1f)] public float FadeSeconds = 0.16f;

    [Header("Placement")]
    [Tooltip("Shift along the sweep's own normal, so the stroke sits just off " +
        "the blade and reads as the weapon's wake rather than paint over the " +
        "weapon. Keep it small; its sign picks which face of the swing it sits on.")]
    [Range(-0.3f, 0.3f)] public float DepthOffset = -0.02f;

    [Tooltip("Separation between the body, the highlight and the accents, " +
        "along that same normal.")]
    [Range(0f, 0.12f)] public float LayerSeparation = 0.012f;

    /// <summary>Resolved highlight numbers, whether preset or hand-authored.</summary>
    public HighlightSpec ResolveHighlight()
    {
        return HighlightShape switch
        {
            // Long, narrow, sharp at both ends, hugging the tip's path.
            SlashHighlightShape.ThinBlade =>
                new HighlightSpec(0.26f, 0.02f, 0.9f, 0.32f, 2.4f, 0f),

            // Balanced: widest just behind the blade, moderate point.
            SlashHighlightShape.Sword =>
                new HighlightSpec(0.45f, 0.04f, 0.8f, 0.4f, 1.4f, 0.05f),

            // Swells late, so the stroke reads as widening into the cut.
            SlashHighlightShape.Axe =>
                new HighlightSpec(0.62f, 0.06f, 0.72f, 0.6f, 1.1f, 0.03f),

            // Short, thick and blunt: mass rather than edge.
            SlashHighlightShape.Mace =>
                new HighlightSpec(0.78f, 0.04f, 0.5f, 0.45f, 0.7f, 0f),

            _ => new HighlightSpec(
                HighlightWidth, HighlightStart, HighlightLength,
                HighlightBias, HighlightSharpness, HighlightOffset)
        };
    }

    public readonly struct HighlightSpec
    {
        /// <summary>Fraction of the stroke's width.</summary>
        public readonly float Width;
        /// <summary>Where the streak starts along the trail, 0 at the blade.</summary>
        public readonly float Start;
        /// <summary>How much of the trail it covers.</summary>
        public readonly float Length;
        /// <summary>Where it swells, 0 to 1 within its own span.</summary>
        public readonly float Bias;
        /// <summary>How pointed its ends are.</summary>
        public readonly float Sharpness;
        /// <summary>How far in from the tip's edge it sits.</summary>
        public readonly float Offset;

        public HighlightSpec(
            float width, float start, float length,
            float bias, float sharpness, float offset)
        {
            Width = width;
            Start = start;
            Length = length;
            Bias = bias;
            Sharpness = sharpness;
            Offset = offset;
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        // A streak that runs past the end of the trail it rides on would be
        // clipped mid-taper, which is exactly the hard cap the shape work is
        // there to avoid.
        HighlightLength = Mathf.Min(HighlightLength, 1f - HighlightStart);
        AccentLength = Mathf.Min(AccentLength, 1f - AccentStart);
    }
#endif
}
