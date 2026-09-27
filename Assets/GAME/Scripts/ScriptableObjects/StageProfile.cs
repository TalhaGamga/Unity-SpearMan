using UnityEngine;

/// <summary>
/// Every dial of a showcase stage, in one asset.
///
/// The stage has one job: make a character and the effect it is swinging the
/// most legible things on screen. Everything here is in service of that, and
/// most of it works by subtraction - the environment is built to be seen past
/// rather than seen.
///
/// Three rules run through the numbers below, and they are worth stating
/// because they are what makes a preset readable rather than merely dark:
///
/// The background is darkest where the subject stands. A bright horizon band
/// sits a little below eye level, so the character is framed against the
/// deepest part of the gradient and its silhouette needs no outline to read.
///
/// Nothing in the environment has an edge. The ground fades into the fog,
/// the fog is the horizon's own colour, and the sky has no feature in it. A
/// visible horizon line or a plane's far edge would give the eye somewhere
/// else to settle, and there is nowhere else worth settling.
///
/// Light is spent on the subject, not the room. The key is soft and the fill
/// is dim; what separates the character from the background is the rim, which
/// costs nothing anywhere else because it only lands on silhouette edges.
///
/// The effects then supply the only saturated colour in frame, which is why
/// bloom is worth having here and would be a mistake in a dressed level.
/// </summary>
[CreateAssetMenu(
    menuName = "ScriptableObjects/Stage/Stage Profile",
    fileName = "StageProfile")]
public class StageProfile : ScriptableObject
{
    [Header("Sky")]
    [Tooltip("Colour straight overhead. The darkest part of the backdrop, so " +
        "anything raised into it - a weapon at the top of a swing, an effect " +
        "thrown upward - reads against it without help.")]
    [ColorUsage(false, true)]
    public Color SkyZenith = new Color(0.055f, 0.075f, 0.105f);

    [Tooltip("Colour at the horizon, and the brightest thing in the scene " +
        "after the subject. The fog takes this colour too, which is what makes " +
        "the ground dissolve into the distance instead of ending somewhere.")]
    [ColorUsage(false, true)]
    public Color SkyHorizon = new Color(0.50f, 0.56f, 0.62f);

    [Tooltip("Colour straight down. Kept near black so the lower half of frame " +
        "stays empty and the ground never announces itself.")]
    [ColorUsage(false, true)]
    public Color SkyNadir = new Color(0.015f, 0.02f, 0.03f);

    [Tooltip("Where the bright band sits, as a fraction of the way up the sky. " +
        "A little above zero puts it just under eye level, which frames a " +
        "standing character against the dark rather than against the light.")]
    [Range(-1f, 1f)] public float HorizonHeight = 0.02f;

    [Tooltip("How far the band spreads either side of that. Wide is haze; " +
        "narrow starts to read as a horizon line, which is the one thing the " +
        "backdrop is trying not to have.")]
    [Range(0.01f, 2f)] public float HorizonSoftness = 0.55f;

    [Tooltip("How quickly the sky falls off to the zenith colour.")]
    [Range(0.1f, 6f)] public float ZenithFalloff = 1.1f;

    [Tooltip("How quickly it falls off downward to the nadir colour.")]
    [Range(0.1f, 6f)] public float NadirFalloff = 1.6f;

    [Tooltip("Overall brightness of the backdrop. This is the main control " +
        "for how far the environment sits behind the subject: lower it until " +
        "the character is clearly the brightest thing on screen.")]
    [Range(0f, 4f)] public float SkyExposure = 1f;

    [Header("Fog")]
    [Tooltip("Dissolves the ground into the backdrop. Without it the ground " +
        "plane ends at a hard edge somewhere in frame, and that edge is the " +
        "first thing the eye finds.")]
    public bool FogEnabled = true;

    [Tooltip("Leave this on to keep the fog locked to the horizon colour, " +
        "which is what makes the ground vanish rather than merely dim. Turn it " +
        "off only to tint the distance away from the sky on purpose.")]
    public bool FogMatchesHorizon = true;

    [Tooltip("Used when the fog is not following the horizon.")]
    [ColorUsage(false, true)]
    public Color FogColor = new Color(0.16f, 0.19f, 0.23f);

    [Tooltip("How thick the air is. Higher pulls the vanishing point in close " +
        "and shrinks the stage to the ground the subject is standing on.")]
    [Range(0.001f, 0.2f)] public float FogDensity = 0.022f;

    [Header("Ground")]
    [Tooltip("Draw a ground plane at all. Off leaves the subject floating in " +
        "the gradient, which suits an effect that wants no reference at all.")]
    public bool GroundEnabled = true;

    [Tooltip("Base colour of the ground. Near black on purpose: the ground is " +
        "meant to be read only where an effect lights it, so that the light an " +
        "attack throws is the thing that reveals the floor.")]
    [ColorUsage(false, true)]
    public Color GroundColor = new Color(0.014f, 0.017f, 0.022f);

    [Tooltip("How polished the floor is. A little gives the sheen that lets a " +
        "bright effect smear across it; too much turns the floor into a mirror " +
        "and puts a second copy of the effect in frame.")]
    [Range(0f, 1f)] public float GroundSmoothness = 0.55f;

    [Tooltip("How metallic the floor reads. Low keeps the sheen broad and " +
        "colourless rather than a tight reflected highlight.")]
    [Range(0f, 1f)] public float GroundMetallic = 0.1f;

    [Tooltip("Width of the plane in metres. It only has to reach past where " +
        "the fog closes; beyond that it is invisible geometry.")]
    [Range(10f, 400f)] public float GroundSize = 120f;

    [Tooltip("Height of the floor. The subject stands on this, so it is also " +
        "where the stage's shadows land.")]
    public float GroundHeight;

    [Header("Key Light")]
    [Tooltip("The light that shapes the character. Soft and slightly off to " +
        "one side, so the form turns rather than flattening out.")]
    [ColorUsage(false, true)]
    public Color KeyColor = new Color(1f, 0.96f, 0.91f);

    [Range(0f, 8f)] public float KeyIntensity = 1.35f;

    [Tooltip("How high the key sits, in degrees. Around forty reads as " +
        "daylight without losing the face to its own brow shadow.")]
    [Range(0f, 90f)] public float KeyPitch = 42f;

    [Tooltip("Where it sits around the subject, in degrees, with zero behind " +
        "the camera. Off to one side gives the form somewhere to turn.")]
    [Range(-180f, 180f)] public float KeyYaw = -35f;

    [Tooltip("Cast shadows from the key. A contact shadow under the character " +
        "is most of what stops it reading as pasted onto the backdrop.")]
    public bool KeyCastsShadows = true;

    [Tooltip("How dark those shadows go. Full black is rarely right here - the " +
        "fill is meant to be doing some of this work.")]
    [Range(0f, 1f)] public float KeyShadowStrength = 0.65f;

    [Header("Fill Light")]
    [Tooltip("Opposite the key, and much dimmer. Its whole job is to keep the " +
        "shadow side from going to nothing, so the silhouette stays a shape " +
        "rather than a hole.")]
    [ColorUsage(false, true)]
    public Color FillColor = new Color(0.42f, 0.52f, 0.68f);

    [Range(0f, 4f)] public float FillIntensity = 0.35f;

    [Range(-20f, 90f)] public float FillPitch = 18f;

    [Range(-180f, 180f)] public float FillYaw = 130f;

    [Header("Rim Light")]
    [Tooltip("From behind the subject, catching the edges that face away. " +
        "This is what separates the character from the backdrop, and it is the " +
        "cheapest separation there is - it only lands on silhouette edges, so " +
        "it costs nothing anywhere else in frame.")]
    [ColorUsage(false, true)]
    public Color RimColor = new Color(0.62f, 0.76f, 1f);

    [Range(0f, 8f)] public float RimIntensity = 1.6f;

    [Tooltip("Low is usually right: a rim from near the subject's own height " +
        "runs down the whole outline, while a steep one only catches shoulders.")]
    [Range(-20f, 90f)] public float RimPitch = 12f;

    [Tooltip("Behind the subject rather than exactly opposite the camera, so " +
        "the edge it draws is on one side and the shape stays asymmetric.")]
    [Range(-180f, 180f)] public float RimYaw = 168f;

    [Header("Ambient")]
    [Tooltip("Sky end of the ambient gradient - what light the environment " +
        "throws onto upward-facing surfaces.")]
    [ColorUsage(false, true)]
    public Color AmbientSky = new Color(0.14f, 0.17f, 0.22f);

    [Tooltip("The band around the horizon, which is what most of a standing " +
        "character actually catches.")]
    [ColorUsage(false, true)]
    public Color AmbientEquator = new Color(0.08f, 0.09f, 0.12f);

    [Tooltip("Bounce from the floor. Kept dark, because a bright one lifts the " +
        "whole scene and flattens everything the key just shaped.")]
    [ColorUsage(false, true)]
    public Color AmbientGround = new Color(0.02f, 0.022f, 0.028f);

    [Tooltip("Scales all three. The single fastest control for how much the " +
        "environment is allowed to say.")]
    [Range(0f, 3f)] public float AmbientIntensity = 1f;

    [Header("Post Processing")]
    [Tooltip("Let the stage drive its own colour grading and bloom. Off leaves " +
        "whatever volumes the scene already has alone.")]
    public bool PostProcessing = true;

    [Tooltip("How much the bright parts of an effect spill into the air around " +
        "them. This is what an attack looks like on this stage, and it is only " +
        "affordable because nothing in the environment is bright enough to " +
        "bloom - in a dressed level the same number would fog the whole frame.")]
    [Range(0f, 3f)] public float BloomIntensity = 0.85f;

    [Tooltip("How bright a pixel has to be before it spills. Above the " +
        "backdrop and below the effects, so only the effects glow.")]
    [Range(0f, 4f)] public float BloomThreshold = 0.95f;

    [Tooltip("How far the spill spreads.")]
    [Range(0f, 1f)] public float BloomScatter = 0.62f;

    [Tooltip("Darkens the corners. Pulls the eye to the middle of frame, where " +
        "the subject is, and quietly hides that the stage does not extend far.")]
    [Range(0f, 1f)] public float VignetteIntensity = 0.34f;

    [Range(0.01f, 1f)] public float VignetteSmoothness = 0.55f;

    [Tooltip("Overall exposure after grading, in stops.")]
    [Range(-3f, 3f)] public float PostExposure;

    [Tooltip("Contrast. A little lift deepens the backdrop without touching " +
        "the subject, which is most of what makes the stage read as moody " +
        "rather than merely underlit.")]
    [Range(-100f, 100f)] public float Contrast = 12f;

    [Tooltip("Saturation. Negative is usually right: draining the environment " +
        "leaves the effects as the only real colour in frame.")]
    [Range(-100f, 100f)] public float Saturation = -14f;

    /// <summary>The colour the distance actually fogs to.</summary>
    public Color ResolvedFogColor => FogMatchesHorizon ? SkyHorizon : FogColor;

    /// <summary>
    /// A light's rotation from a pitch and a yaw, with yaw measured from
    /// behind the camera so the numbers read the way a photographer would set
    /// them up rather than as raw world angles.
    /// </summary>
    public static Quaternion Orientation(float pitch, float yaw)
    {
        return Quaternion.Euler(pitch, yaw, 0f);
    }
}
