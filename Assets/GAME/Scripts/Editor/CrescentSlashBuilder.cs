using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace CombatEditor
{
    /// <summary>
    /// Builds the crescent slash - its mesh, its seven materials, its prefab and
    /// the <see cref="SlashProfile"/>s that point at it - straight from code,
    /// along with the small hit impact that plays on a confirmed hit (its own
    /// prefab and materials, and the "Hit" cues in the sword's pack that
    /// spawn it).
    ///
    /// The effect is a stack of layers drawn on the same geometry: a soft glow,
    /// a toon brush stroke on the inside radius, the violet body, and four
    /// sprays around them. Every layer shares one crescent (or, in the game, one
    /// band built from the blade's path), and what makes them read as separate
    /// strokes sliding against each other is the window each material carves
    /// out of it. So the geometry is simple and fixed, and the look lives in
    /// numbers - which is why the numbers live in one block,
    /// <see cref="Look"/>, and everything else here is plumbing that must not
    /// need touching when the art changes.
    ///
    /// Built rather than hand-authored because a hand-authored particle prefab
    /// drifts: an inspector click enables a module nobody meant to use, a
    /// default Cone shape stays on, a lifetime is left at five seconds. A build
    /// states every module on purpose, and a rebuild restates it.
    ///
    /// Rebuilding is safe to repeat. Every asset keeps its GUID: the mesh is
    /// refilled in place, the materials are rewritten onto the objects that
    /// already exist, and the prefab is saved over its own path, so every cue
    /// and profile that references them keeps working. Materials and the
    /// profile keep whatever the art pass tuned unless a reset is asked for;
    /// the mesh and the prefabs are always rebuilt, because they are structure
    /// rather than taste. The pack is only ever added to: a missing hit cue is
    /// appended, an existing cue is never touched.
    ///
    /// No dialogs and nothing that reads the selection, so it runs unattended
    /// under <c>-batchmode -executeMethod CombatEditor.CrescentSlashBuilder.BuildReset</c>.
    /// </summary>
    public static class CrescentSlashBuilder
    {
        #region Look

        /// <summary>
        /// EVERY TUNABLE NUMBER OF THE SLASH, AND NOTHING ELSE.
        ///
        /// The art pass edits this block and only this block. Timings are in
        /// seconds, sizes in the effect's local units (the blade tip sits at
        /// radius 1, the Route-B ArcRadius), colours are HDR - values above 1
        /// are what drive bloom.
        ///
        /// Materials list only what differs from the shader's own defaults,
        /// except Body, which spells out every property: the shader defaults
        /// were chosen for the body, and writing them down here keeps this block
        /// the single source of truth even if someone edits the shader.
        ///
        /// Material values are applied on a first build and on
        /// "Rebuild And Reset Materials"; a plain Build leaves tuned materials
        /// alone. Everything else here is applied on every build.
        /// </summary>
        private static class Look
        {
            // ------------------------------------------------------------ Timing

            /// <summary>
            /// How long the shape layers take to sweep in from tail to head. The
            /// shader's HIDE input runs from <see cref="RevealStartHide"/> to 0
            /// over this, eased out (see <see cref="RevealEase"/>), and the
            /// sparks' and motes' emitter strips are laid through the same ramp,
            /// so everything follows one sweep.
            /// </summary>
            public const float RevealSeconds = 0.09f;

            /// <summary>
            /// Where HIDE starts. The first stretch of the crescent is the thin,
            /// frayed brush tail and nothing of the body, so a reveal from 1
            /// would spend its first frames showing nothing and land late;
            /// starting here puts a real chunk of stroke on frame one.
            /// </summary>
            public const float RevealStartHide = 0.72f;

            /// <summary>
            /// The reveal's ease-out exponent: HIDE = start * (1 - progress)^n.
            /// 2 snaps out of the tail at twice the average speed and settles
            /// into the head over the last frames - the smear-then-land read,
            /// and the same shape as a real swing's ease (Route A). 1 is the
            /// old constant speed. Every Loop emitter vertex is placed through
            /// the same function (revealHeadT), so the sparks stay on the head
            /// whatever this is.
            /// </summary>
            public const float RevealEase = 2f;

            /// <summary>
            /// How far the drawn head trails the HIDE curve. A particle's
            /// custom data is evaluated at the start of its update step, so the
            /// crescent shows the head where it was one step ago - a frame in
            /// the game (0.017 s), up to 0.03 s under the lab's Simulate
            /// (measured: HIDE 0.495 at 0.05 s where the curve reads 0.27).
            /// Emission positions are exact, so the sparks' strip is laid this
            /// far back to shed from the head that is on screen, not ahead of it.
            /// </summary>
            public const float HeadSampleLag = 0.022f;

            /// <summary>Every system's duration. Longer than any lifetime, so nothing is cut off.</summary>
            public const float Duration = 0.6f;

            // ------------------------------------------------------------ Crescent mesh

            public const float SpanDegrees = 320f;
            public const float HeadAngleDegrees = 0f;

            // Same v-to-radius mapping as the Route-A band, derived from it
            // rather than restated (ShapeInnerAlongBlade .. ShapeOuterAlongBlade,
            // the tip on radius 1), so a window tuned on one lands at the same
            // radii on the other - the materials are tuned on the band, which is
            // what the game draws, and the standalone prefab follows. With the
            // band ending at the tip, the crescent's outer edge is the blade's
            // real reach too. The tail closes to 0.92 of the head's radius, so
            // the stroke winds visibly inside itself - a spiral, not a ring.
            public const float HeadOuter = ShapeOuterAlongBlade;
            public const float TailRadiusScale = 0.92f;
            public const float InnerRatio = ShapeInnerAlongBlade / ShapeOuterAlongBlade;
            public const float Lift = 0.10f;
            public const int SegmentsAlong = 160;
            public const int SegmentsAcross = 10;

            // ------------------------------------------------------------ Shape layers
            // Hierarchy order is draw order: Glow (root), Brush, Body.
            //
            // Life curves are snap-then-linger: a hold of about five frames on
            // the landed key pose (the only thing that reads at game distance),
            // then the tail and inner bulk go fast, and the rim-and-head remnant
            // lingers and breaks up. The glow recedes first.
            //
            // Readability (in game, over the characters): the band is a thin
            // hollow ribbon on its outer edge, not a sheet. Bloom: the
            // sandbox's threshold of 0.95 is a gamma value, which URP turns
            // into 0.89 linear with a soft knee from about 0.45 - so a resting
            // colour, times (1 + MarbleTone) plus Highlight x StreakBright, is
            // kept at or under about 0.85, and only the hot rim, the head, the
            // sparks and the hit impact bloom. The hot rim, the head and the
            // glow are scaled by _Hot, which SlashEffect drives from the
            // attack's damage window: the brightest accent sits on the leading
            // edge only while the blade can hurt, dies within HotDecaySeconds
            // after, and is held to HotAfterHit once a hit has landed, so the
            // impact on the target is the brightest thing on a hit. Band laid
            // outside the window shrinks to one cold line on the outer edge -
            // the body's boundary; the brush and the glow drop out there
            // (_OutsideLayer 0) - which is how the reach stays readable without
            // a wide surface. Silhouette fade clears each sheet where a
            // character stands just behind it (in full over a body's own
            // depth, _SilhouetteHold), sparing only the rim line - cooled
            // there to the cold boundary (_SilhouetteRimCool) - so heads,
            // blades and poses read through the stroke. As a stopped stroke
            // erodes its value falls too (_LifeDim), so the remnant steps
            // back behind the blade and the reacting target.
            //
            // Colour against the fighters: the enemy is turquoise (#0A98C0,
            // hue 193) and the hero crimson. The violet body and its boundary
            // contrast with both. The brush's turquoise is a greener, darker
            // jade (hue ~170, about half the enemy's value) with ultramarine
            // patches, so a stroke beside the enemy never reads as more enemy.

            public static readonly ShapeLayerSpec Glow = new ShapeLayerSpec
            {
                Name = "Glow",
                Lifetime = 0.34f,
                Life = Alpha((0f, 1f), (0.38f, 1f), (1f, 0f)),
                Material = new MaterialSpec
                {
                    Name = "SlashL_Glow",
                    Shader = LayeredShader,
                    Queue = 3000,
                    Props = new[]
                    {
                        // A faint lift on the body's outer rim and head, not a
                        // sheet over the interior: the dark between the tongues
                        // and the brush strands is what separates the layers.
                        // Only over the head's last 10% - a halo framing the
                        // tip, not the leading third: over the last 30% it
                        // lifted a long pale-lilac stretch of the arc past the
                        // threshold - only on the outer 16% across - a wider,
                        // softer halo laid a blooming haze along the enemy's
                        // back on the rising cut - and only while the hit
                        // window is hot (HotCoverage 1): a halo along the
                        // whole arc, held through the fade, was the
                        // end-of-swing flare that out-shone the weapon and the
                        // hit point.
                        V("_Across", 0.84f, 1.0f),
                        F("_Anchor", 1f),
                        F("_TailCut", 0.9f),
                        F("_TailWidth", 0.4f),
                        F("_WidthPeak", 0.8f),
                        F("_HeadWidth", 1.0f),
                        F("_TaperPower", 1.0f),
                        F("_HeadCap", 0.07f),
                        F("_HeadCapWidth", 1.0f),
                        // Soft: full at the body's rim, falling smoothly to
                        // nothing at the mesh edge, and (s*2 along) brightest
                        // at the head.
                        F("_EdgeSoft", 0.3f),
                        F("_InnerRough", 0f),
                        F("_OuterRough", 0.0f),
                        F("_TailFray", 0.2f),
                        V("_NoiseScale", 1.6f, 5f),
                        F("_NoiseWarp", 0.4f),
                        F("_Seed", 3f),
                        // Lavender-violet, white-lavender at the head: the
                        // head bloom the game's weak bloom cannot make.
                        C("_HeadColor", 1.2f, 0.55f, 2.0f),
                        C("_MidColor", 0.55f, 0.15f, 1.6f),
                        C("_TailColor", 0.15f, 0.1f, 1.3f),
                        C("_ShadowColor", 0f, 0f, 0f),
                        F("_InnerShade", 0f),
                        F("_StreakDark", 0f),
                        F("_StreakBright", 0f),
                        C("_RimColor", 1.5f, 1.0f, 2.2f),
                        F("_RimWidth", 0f),
                        F("_HeadHot", 0.6f),
                        F("_HeadHotPower", 14f),
                        // Its colours are the only resting ones above 0.9, and
                        // they may be: the layer exists only while hot, and
                        // these two keep what it adds to a faint lift round
                        // the tip - the hot rim blooms on its own.
                        F("_Intensity", 0.8f),
                        F("_Opacity", 0.25f),
                        F("_AlphaNoise", 0.15f),
                        F("_Additive", 1.0f),
                        F("_HotCoverage", 1f),
                        F("_SilhouetteFade", 0.9f),
                        F("_SilhouetteDepth", 1.0f),
                        F("_SilhouetteHold", 0.45f),
                        // None outside the damage window: the body's boundary
                        // is the one line there.
                        F("_OutsideLayer", 0f),
                        // Recedes towards the head and is gone early, so the
                        // hard fragments finish on a clean background.
                        F("_DissolveNoise", 0.15f),
                        F("_DissolveTail", 1.0f),
                        F("_DissolveInner", 0f),
                        F("_DissolveSoft", 0.15f),
                        F("_BurnWidth", 0f),
                        V("_DissolveNoiseScale", 1.5f, 4f),
                        F("_DissolveBias", 0.3f),
                        // The halo thins with the band instead of holding full
                        // strength until its last fragment goes.
                        F("_FadeOpacity", 0.4f)
                    }
                }
            };

            public static readonly ShapeLayerSpec Brush = new ShapeLayerSpec
            {
                Name = "Brush",
                Lifetime = 0.5f,
                Life = Alpha((0f, 1f), (0.34f, 1f), (0.62f, 0.4f), (1f, 0f)),
                Material = new MaterialSpec
                {
                    Name = "SlashL_Brush",
                    Shader = LayeredShader,
                    Queue = 3001,
                    Props = new[]
                    {
                        // The body's under-stroke, not a second arc: tucked
                        // under the body's ragged inner tongues (0.4-0.72
                        // across, the body starting at 0.52), on the head end
                        // of the stroke only (TailCut, a steep swell), so the
                        // slash is one violet-over-turquoise ribbon with its
                        // inner quarter left open. Run the whole length at
                        // 0.15-0.6 it was a second reach line and traced the
                        // enemy's back at hit range.
                        V("_Across", 0.4f, 0.72f),
                        F("_Anchor", 1.0f),
                        F("_TailCut", 0.2f),
                        F("_TailWidth", 0.1f),
                        F("_WidthPeak", 0.75f),
                        F("_HeadWidth", 0.95f),
                        F("_TaperPower", 1.5f),
                        // Drifts out under the body's needle as it goes back,
                        // so along the older arc the jade meets the violet's
                        // underside as one boundary. At 0.16 a 0.08-wide gap
                        // opened between them and the under-stroke read as a
                        // second, inner reach line. The head is unchanged:
                        // the drift scales with the distance from it.
                        F("_TailDrift", 0.24f),
                        F("_HeadFray", 0.035f),
                        // Bold pointed tongues about 4:1, swept back towards
                        // the tail, not hairline strands.
                        F("_EdgeSoft", 0.015f),
                        F("_InnerRough", 0.45f),
                        F("_OuterRough", 0.15f),
                        F("_TailFray", 0.35f),
                        V("_EdgeNoiseScale", 9f, 6f),
                        F("_EdgeSkew", 0.4f),
                        // Big flat toon patches that never swim; long curved
                        // azure tongues about 10:1.
                        V("_NoiseScale", 2.0f, 4.5f),
                        F("_NoiseScroll", 0f),
                        F("_NoiseWarp", 0.9f),
                        V("_WarpScale", 1.5f, 0.5f),
                        F("_WarpScroll", 1f),
                        F("_Seed", 7f),
                        // Ultramarine head, jade-turquoise from there: hue ~170
                        // against the enemy's 193 and about two-thirds of its
                        // value (mid sRGB ~(0,130,112) against (10,152,192)),
                        // so the turquoise stays in the palette but figure and
                        // ground separate by hue and by value. The old
                        // cyan-teal (0, 0.2, 0.36) was the enemy's own hue a
                        // step darker and read as more enemy wherever it ran
                        // beside it; half the value (0, 0.15, 0.11) went dark
                        // enough that the turquoise all but left the slash.
                        C("_HeadColor", 0.02f, 0.1f, 0.5f),
                        C("_MidColor", 0.0f, 0.22f, 0.16f),
                        C("_TailColor", 0.0f, 0.18f, 0.13f),
                        F("_MidPoint", 0.7f),
                        C("_ShadowColor", 0.0f, 0.08f, 0.07f),
                        F("_InnerShade", 0.45f),
                        F("_InnerShadePower", 1.2f),
                        // Ultramarine patches on the side next to the body,
                        // deep enough that the enemy is always the lighter shape.
                        F("_StreakDark", 1.0f),
                        F("_StreakDarkThreshold", 0.38f),
                        C("_StreakDarkColor", 0.03f, 0.1f, 0.5f),
                        F("_StreakBias", -0.35f),
                        F("_StreakBright", 0.4f),
                        F("_StreakBrightThreshold", 0.8f),
                        C("_HighlightColor", 0.08f, 0.42f, 0.3f),
                        F("_StreakSoft", 0.008f),
                        F("_Posterize", 3f),
                        // Dry-brush marks where the paint later opens up.
                        F("_Bristle", 0.6f),
                        F("_BristleThreshold", 0.66f),
                        F("_RimWidth", 0f),
                        F("_HeadHot", 0f),
                        F("_Intensity", 1f),
                        // Half see-through, and a third of it light rather
                        // than paint, so a body or a blade behind the brush
                        // shows through instead of being painted out.
                        F("_Opacity", 0.55f),
                        F("_AlphaNoise", 0f),
                        F("_Additive", 0.3f),
                        // Recedes with the heat to 55%: once the window has
                        // shut, the frames belong to the hit reaction, and the
                        // violet body and its boundary already carry the
                        // direction and the reach. Not further: at 0.6 the
                        // turquoise was all but gone on every hit.
                        F("_HotCoverage", 0.45f),
                        F("_SilhouetteFade", 0.9f),
                        F("_SilhouetteDepth", 1.0f),
                        F("_SilhouetteHold", 0.45f),
                        // None outside the damage window: the body's boundary
                        // is the one line there.
                        F("_OutsideLayer", 0f),
                        // Darkens towards half value as it erodes, with the
                        // body, so the stopped stroke steps back.
                        F("_LifeDim", 0.5f),
                        // Goes first and from the inside, so the last shards
                        // hug the violet body rather than the enemy. No burn:
                        // any light band on paint this close to the enemy's
                        // value left pale flecks on its back as it faded.
                        F("_DissolveNoise", 0.6f),
                        F("_DissolveTail", 0.9f),
                        F("_DissolveInner", 0.9f),
                        // Soft-edged erosion, and half the fade spent thinning:
                        // the shards melt away rather than blinking off.
                        F("_DissolveSoft", 0.05f),
                        C("_BurnColor", 0.05f, 0.3f, 0.2f),
                        F("_BurnWidth", 0f),
                        V("_DissolveNoiseScale", 6f, 10f),
                        // No early bias any more: with the eased fade the
                        // erosion runs to the end of life, and the last shards
                        // fade (_FadeOpacity) instead of being cut at 40%.
                        F("_DissolveBias", 0f),
                        F("_FadeOpacity", 0.7f)
                    }
                }
            };

            public static readonly ShapeLayerSpec Body = new ShapeLayerSpec
            {
                Name = "Body",
                Lifetime = 0.48f,
                Life = Alpha((0f, 1f), (0.36f, 1f), (0.62f, 0.42f), (1f, 0f)),
                Material = new MaterialSpec
                {
                    Name = "SlashL_Body",
                    Shader = LayeredShader,
                    Queue = 3002,
                    Props = new[]
                    {
                        // Window: a thin hollow ribbon hugging the outer edge
                        // (0.52-1.0 across, anchored outside - about 0.26 of
                        // the blade's reach at most), swelling steeply towards
                        // the head and tapering over the older arc to a needle
                        // that ends as the boundary line, so even the cold
                        // remnant is a comma pointing along the swing. The
                        // inside of the band is left open, so the characters
                        // under the swing stay visible and the brush shows as
                        // turquoise tongues under the inner edge.
                        V("_Across", 0.52f, 1.0f),
                        F("_Anchor", 1f),
                        F("_TailCut", 0.22f),
                        F("_TailWidth", 0.08f),
                        F("_WidthPeak", 0.7f),
                        F("_HeadWidth", 0.8f),
                        F("_TaperPower", 1.6f),
                        F("_TailDrift", 0f),
                        // The head front is the blade's cut: hard, straight
                        // along the blade, only its outer 40% rounded.
                        F("_HeadSoft", 0f),
                        F("_HeadCap", 0.05f),
                        F("_HeadCapWidth", 0.4f),
                        F("_HeadFray", 0f),
                        // Edges: clean outer rim, bold flame-like inner tongues
                        // leaning back towards the tail.
                        F("_EdgeSoft", 0.03f),
                        F("_InnerRough", 0.55f),
                        F("_OuterRough", 0.03f),
                        F("_TailFray", 0.2f),
                        V("_EdgeNoiseScale", 8f, 5f),
                        F("_EdgeSkew", 0.25f),
                        // Noise: about five bold streaks across the head, long
                        // along the stroke (~2.5 world), the warp bending them
                        // once per streak length into S-folds and drifting at
                        // 0.3 of their flow, so the liquid pours through
                        // slow-moving bends. A shorter warp (2.5 along) curled
                        // them into isotropic worms (r5 first pass).
                        V("_NoiseScale", 2.4f, 5.5f),
                        F("_NoiseScroll", 1.2f),
                        F("_NoiseWarp", 1.0f),
                        F("_Seed", 0f),
                        V("_WarpScale", 1.0f, 0.45f),
                        F("_WarpScroll", 0.3f),
                        // Colour: pink only as the band between the tip and
                        // the violet, which carries most of the arc. At rest
                        // nothing of the body blooms: the brightest resting
                        // point - head blue 0.62 x (1 + MarbleTone) plus the
                        // highlight 0.42 x StreakBright - is about 0.86,
                        // under URP's real 0.89, where 0.9 x 1.3 + 0.4 made a
                        // pastel lilac one step below the hot rim. The light
                        // is left to the rim, a 2.5x value step above.
                        C("_HeadColor", 0.55f, 0.14f, 0.62f),
                        C("_MidColor", 0.3f, 0.04f, 0.6f),
                        C("_TailColor", 0.1f, 0.02f, 0.42f),
                        F("_MidPoint", 0.82f),
                        C("_ShadowColor", 0.05f, 0.02f, 0.25f),
                        F("_InnerShade", 0.25f),
                        F("_InnerShadePower", 2.5f),
                        // Bold indigo tongues out of the inner side, thin
                        // pink ribbons gathering towards the rim.
                        F("_StreakDark", 0.85f),
                        F("_StreakDarkThreshold", 0.38f),
                        C("_StreakDarkColor", 0.11f, 0.05f, 0.6f),
                        F("_StreakBias", 0.5f),
                        F("_StreakBright", 0.35f),
                        F("_StreakBrightThreshold", 0.6f),
                        C("_HighlightColor", 0.32f, 0.14f, 0.42f),
                        F("_HighlightRibbon", 0.15f),
                        F("_StreakSoft", 0.015f),
                        F("_Posterize", 0f),
                        F("_Bristle", 0f),
                        F("_BristleThreshold", 0.66f),
                        F("_MarbleTone", 0.15f),
                        // Rim: white-hot, but only on the leading edge (no rim
                        // at the tail, weighted steeply to the head - a^12 is
                        // a quarter by 0.89 along, about the last half unit
                        // of arc) and only while the hit window is hot - the
                        // shader scales it by _Hot. A near-neutral white, not
                        // lilac: with green well under red and blue the
                        // tonemapped, desaturated result read pale lilac, one
                        // step from the resting violet, and over 5 the heat
                        // spread over an 85-130 px stretch that the spray dots
                        // behind it could tie. The head's hot spot is held to
                        // the last ~6% of the stroke and its outer corner
                        // (HeadHotAcross 3), so the accent is the blade's tip,
                        // and strong there (HeadHot 1.6): the head cap rounds
                        // off the very corner, and at 0.7 what was left of it
                        // read only a pale lilac a step over the body on a
                        // whiff. White for the four frames the window is open,
                        // and on a hit held under the impact's core by
                        // HotAfterHit and cooled over the target (RimCool,
                        // below). The same rim mask carries a cold
                        // violet boundary along the whole arc underneath, so
                        // the reach still reads once the hot edge has died;
                        // violet is the one colour that always contrasts with
                        // the turquoise enemy, held just under the threshold.
                        C("_RimColor", 2.4f, 2.2f, 2.8f),
                        F("_RimWidth", 0.07f),
                        F("_RimMinWidth", 0.012f),
                        F("_RimSoft", 0.025f),
                        F("_RimTail", 0f),
                        F("_RimPower", 12f),
                        F("_HeadHot", 1.6f),
                        F("_HeadHotPower", 12f),
                        F("_HeadHotAcross", 3f),
                        F("_RimDark", 0.55f),
                        F("_RimDarkWidth", 0.14f),
                        C("_BoundaryColor", 0.5f, 0.16f, 0.8f),
                        F("_BoundaryStrength", 1f),
                        // Opacity: three-quarters covering, a quarter of that
                        // light rather than paint. HotCoverage 0: the ribbon
                        // itself stays whole whatever _Hot is - only its rim
                        // and head follow the hit window.
                        F("_Intensity", 1f),
                        F("_Opacity", 0.75f),
                        F("_AlphaNoise", 0f),
                        F("_Additive", 0.25f),
                        F("_HotCoverage", 0f),
                        // Clears over a character standing just behind the
                        // ribbon - to a fifth within a body's depth, then
                        // ramping off - so a head or a blade reads through it.
                        // The rim line is spared, and shows where the edge
                        // crossed the body - but cooled there (RimCool 1): its
                        // heat is scaled by the same keep, so over a fighter
                        // it is the cold boundary passing in front, not a
                        // white line along the fighter's contour that reads
                        // as rim light on them and blooms into them.
                        F("_SilhouetteFade", 0.8f),
                        F("_SilhouetteDepth", 1.0f),
                        F("_SilhouetteHold", 0.45f),
                        F("_SilhouetteRimCool", 1f),
                        // The body is the one layer drawn outside the window:
                        // that is the boundary line.
                        F("_OutsideLayer", 1f),
                        // The remnant cools as it erodes, towards 45% of its
                        // value at the end: once the band has stopped, the
                        // blade and the reacting target are what to look at,
                        // and a stopped stroke as light as the live one kept
                        // pulling the eye back to where the cut had been.
                        F("_LifeDim", 0.45f),
                        // Dissolve: from the first fade frame, tail first, so
                        // the remnant pulls back along the swing towards where
                        // it ended and shows the travel; the rim goes last as
                        // the boundary. Shards 6:1, not a comb. No burn band:
                        // on a base this close to the threshold any burn pushed
                        // past it and flared the last visible frame.
                        F("_DissolveNoise", 0.5f),
                        F("_DissolveTail", 0.8f),
                        F("_DissolveInner", 0.7f),
                        // Soft-edged erosion, and half the fade spent thinning,
                        // so the last of the stroke fades out instead of the
                        // final shards vanishing on one hard frame.
                        F("_DissolveSoft", 0.06f),
                        C("_BurnColor", 0.12f, 0.03f, 0.12f),
                        F("_BurnWidth", 0f),
                        V("_DissolveNoiseScale", 6f, 8f),
                        // Negative: the erosion is spread over the whole of the
                        // eased life, down to its last frames. With the old
                        // +0.12 the stroke was gone by 40% life and the fade's
                        // last third showed nothing - the hard end. Measured:
                        // worst one-frame drop of on-screen light 36% -> 16%,
                        // visible for 12 frames instead of 5.
                        F("_DissolveBias", -0.1f),
                        F("_FadeOpacity", 0.7f)
                    }
                }
            };

            // ------------------------------------------------------------ Spray systems
            // Hierarchy order after the shape layers: Embers, Motes, Wisps, Sparks.
            // Emission is always bursts: SlashEffect reads the burst total as each
            // system's weight against the others, and a rate would read as 1.
            // Totals: Embers 5, Motes 6, Wisps 5, Sparks 12 - all above
            // ShapeBurstCount, so none is taken for a shape layer. Embers and
            // wisps sit just above it: they are the lingering, covering debris,
            // and against the sparks' 12 that leaves them the smallest share of
            // the path. Keep every total above ShapeBurstCount when thinning.

            public static readonly SpraySpec Embers = new SpraySpec
            {
                Name = "Embers",
                // After the landing, so no fleck shows ahead of the drawn head.
                BurstTimes = Spread(1.1f * RevealSeconds, 1.6f * RevealSeconds, 5),
                BurstCount = 1,
                Lifetime = new Vector2(0.22f, 0.36f),
                // Negative: short dark dashes peeling backwards along the
                // stroke, the body's own indigo streaks breaking off.
                Speed = new Vector2(-0.6f, -0.2f),
                Size = new Vector2(0.04f, 0.07f),
                ColorA = Color.white,
                ColorB = Color.white,
                Gravity = 0f,
                MaxParticles = 64,
                // Over the body's mid band from where it is wide to the head.
                TangentEmit = true,
                EmitT = new Vector2(0.6f, 0.97f),
                EmitRadius = 0.72f,
                EmitJitter = 0.1f,
                EmitOutwardDegrees = 0f,
                RandomDirection = 0.1f,
                // Used only without TangentEmit.
                Radius = 1.2f,
                RadiusThickness = 0.3f,
                Arc = 230f,
                ArcStart = 230f,
                ArcMode = ParticleSystemShapeMultiModeValue.Random,
                ArcSpeed = 1f,
                RandomPosition = 0f,
                RenderMode = ParticleSystemRenderMode.Stretch,
                VelocityScale = 0.05f,
                LengthScale = 3.5f,
                Drag = 3f,
                SizeOverLife = Line(1f, 0.5f),
                Colors = White(),
                Alphas = Alpha((0f, 0f), (0.1f, 1f), (0.7f, 0.9f), (1f, 0f)),
                NoiseStrength = 0f,
                NoiseFrequency = 1.5f,
                NoiseScroll = 0.5f,
                Material = new MaterialSpec
                {
                    Name = "SlashP_Ember",
                    Shader = SparkShader,
                    Queue = 3003,
                    Props = new[]
                    {
                        C("_Color", 0.05f, 0.02f, 0.2f, 1f),
                        C("_CoreColor", 0.05f, 0.02f, 0.2f, 1f),
                        F("_CoreSize", 0f),
                        F("_Falloff", 3.5f),
                        F("_Additive", 0f),
                        // Covering paint shed backwards along the path, so the
                        // late ones land on the enemy as it reacts: cleared
                        // over a body like the sheets they come off.
                        F("_SilhouetteFade", 0.8f),
                        F("_SilhouetteDepth", 1.0f),
                        F("_SilhouetteHold", 0.45f)
                    }
                }
            };

            public static readonly SpraySpec Motes = new SpraySpec
            {
                Name = "Motes",
                // Laid on the drawn head burst by burst (the same per-burst Loop
                // strip as the sparks), only over the last ~35 degrees of the
                // reveal and the landing, so they pile up at t = 1 as the
                // white-hot follow-through flash. Earlier bursts were left
                // behind by the eased head as white blobs 200 degrees back.
                BurstTimes = new[]
                {
                    0.85f * RevealSeconds, 1.0f * RevealSeconds, 1.1f * RevealSeconds,
                    1.2f * RevealSeconds, 1.35f * RevealSeconds, 1.5f * RevealSeconds
                },
                BurstCount = 1,
                Lifetime = new Vector2(0.07f, 0.14f),
                // Forward along the head's tangent: a small overshoot past it.
                Speed = new Vector2(0.3f, 0.9f),
                // Small, and shrinking as they fade: at 0.3-0.5, and later
                // 0.12-0.2 swelling to 1.4x, they were the largest spray and
                // each one a little flare near the head.
                Size = new Vector2(0.07f, 0.12f),
                ColorA = Color.white,
                ColorB = Color.white,
                Gravity = 0f,
                MaxParticles = 48,
                TangentEmit = true,
                EmitT = new Vector2(1f - RevealStartHide, 1f),
                EmitRadius = 0.82f,
                EmitJitter = 0f,
                EmitOutwardDegrees = 0f,
                EmitLag = 0f,
                RandomDirection = 0.1f,
                // Used only without TangentEmit.
                Radius = 1.2f,
                RadiusThickness = 0.15f,
                Arc = 65f,
                ArcStart = 55f,
                ArcMode = ParticleSystemShapeMultiModeValue.Loop,
                ArcSpeed = 1f,
                RandomPosition = 0.05f,
                RenderMode = ParticleSystemRenderMode.Billboard,
                Drag = 6f,
                SizeOverLife = Line(1f, 0.6f),
                Colors = White(),
                // Pops at full strength on the head and shrinks as it fades.
                Alphas = Alpha((0f, 1f), (0.4f, 0.6f), (1f, 0f)),
                Material = new MaterialSpec
                {
                    Name = "SlashP_Mote",
                    Shader = SparkShader,
                    Queue = 3004,
                    Props = new[]
                    {
                        // Lilac spill round a pale core: a glint, never a
                        // second accent. Every channel under the bloom
                        // threshold on its own; added over the body's violet
                        // the core reaches about 1.3 blue, a clear step under
                        // the blade tip's white. At a 1.1-1.25 core they
                        // bloomed into white dots on the crest behind the
                        // head, and on a whiff one out-shone the tip.
                        C("_Color", 0.42f, 0.15f, 0.52f, 0.5f),
                        C("_CoreColor", 0.72f, 0.5f, 0.85f, 1f),
                        F("_CoreSize", 0.5f),
                        F("_Falloff", 1.5f),
                        F("_Additive", 1f)
                    }
                }
            };

            public static readonly SpraySpec Wisps = new SpraySpec
            {
                Name = "Wisps",
                BurstTimes = Spread(0.6f * RevealSeconds, 1.4f * RevealSeconds, 5),
                BurstCount = 1,
                Lifetime = new Vector2(0.3f, 0.45f),
                // Negative: along the strip's normal reversed, so the flicks
                // peel backwards off the brush's tail end.
                Speed = new Vector2(-1.0f, -0.35f),
                Size = new Vector2(0.025f, 0.042f),
                ColorA = Color.white,
                ColorB = Color.white,
                Gravity = 0f,
                MaxParticles = 64,
                // Tangential, on the brush tail's outer contour, over its
                // frayed last ~20 degrees and a little past its point.
                TangentEmit = true,
                EmitT = new Vector2(-0.06f, 0.07f),
                EmitRadius = 0.89f,
                EmitJitter = 0.08f,
                EmitOutwardDegrees = -8f,
                RandomDirection = 0.08f,
                // Used only without TangentEmit.
                Radius = 1.1f,
                RadiusThickness = 0.15f,
                Arc = 70f,
                ArcStart = 330f,
                ArcMode = ParticleSystemShapeMultiModeValue.Random,
                ArcSpeed = 1f,
                RandomPosition = 0f,
                RenderMode = ParticleSystemRenderMode.Stretch,
                VelocityScale = 0.03f,
                LengthScale = 7f,
                Drag = 7f,
                SizeOverLife = Line(1f, 0.5f),
                Colors = White(),
                // Invisible for the first ~20% of life: in Route A they are laid
                // along the whole path, and surface only once their stretch of
                // stroke has aged - the stroke shreds into flicks as it dies.
                Alphas = Alpha((0f, 0f), (0.18f, 0f), (0.3f, 1f), (0.8f, 0.85f), (1f, 0f)),
                Material = new MaterialSpec
                {
                    Name = "SlashP_Wisp",
                    Shader = SparkShader,
                    Queue = 3005,
                    Props = new[]
                    {
                        // Flat toon paint in the brush's jade, a step lighter
                        // than its tail, not glowing dashes - and not the
                        // enemy's lit cyan, which read as bits of the enemy.
                        C("_Color", 0.02f, 0.2f, 0.14f, 1f),
                        C("_CoreColor", 0.02f, 0.2f, 0.14f, 1f),
                        F("_CoreSize", 0f),
                        F("_Falloff", 0.35f),
                        F("_Additive", 0f),
                        // Covering, like the embers: cleared over a body.
                        F("_SilhouetteFade", 0.8f),
                        F("_SilhouetteDepth", 1.0f),
                        F("_SilhouetteHold", 0.45f)
                    }
                }
            };

            public static readonly SpraySpec Sparks = new SpraySpec
            {
                Name = "Sparks",
                // An impact accent: the second half of the reveal and just past
                // the landing, each spark gone within a few frames.
                BurstTimes = Spread(0.5f * RevealSeconds, 1.3f * RevealSeconds, 6),
                BurstCount = 2,
                Lifetime = new Vector2(0.07f, 0.16f),
                // Fast and heavily dragged: straight zips forward through the
                // head into the tail-head gap, shortening as they slow.
                Speed = new Vector2(2.5f, 6f),
                Size = new Vector2(0.035f, 0.07f),
                ColorA = new Color(1f, 0.95f, 1f, 0.85f),
                ColorB = new Color(0.95f, 0.85f, 1f, 0.7f),
                Gravity = 0f,
                MaxParticles = 128,
                // Tangential from inside the head (white on pink at birth,
                // not hidden in the white rim). Loop mode lays the strip one
                // vertex per particle at the head's position when its burst
                // fires (see fillEmitter), so EmitT is not read here.
                TangentEmit = true,
                EmitT = new Vector2(1f - RevealStartHide, 1f),
                EmitRadius = 0.84f,
                EmitJitter = 0.05f,
                EmitOutwardDegrees = 5f,
                EmitLag = 0.005f,
                RandomDirection = 0.12f,
                // Used only without TangentEmit.
                Radius = 1.25f,
                RadiusThickness = 0.15f,
                Arc = 230f,
                ArcStart = 230f,
                ArcMode = ParticleSystemShapeMultiModeValue.Loop,
                // Circle fallback only (a mesh Loop ignores its speed): just
                // under one lap per reveal. The circle's loop position is the
                // fractional part of time * speed, so at exactly one lap the
                // last burst wraps to phase 0 and fires from the TAIL.
                ArcSpeed = 0.96f / RevealSeconds,
                RandomPosition = 0.03f,
                RenderMode = ParticleSystemRenderMode.Stretch,
                VelocityScale = 0.035f,
                LengthScale = 1.6f,
                Drag = 6f,
                SizeOverLife = EaseIn(1f, 0f),
                // Glints of the rim's light: lavender-white, never magenta.
                Colors = new[]
                {
                    new GradientColorKey(Color.white, 0f),
                    new GradientColorKey(new Color(0.95f, 0.82f, 1f), 0.35f),
                    new GradientColorKey(new Color(0.65f, 0.55f, 1f), 1f)
                },
                Alphas = Alpha((0f, 1f), (0.55f, 1f), (1f, 0f)),
                // No noise and no radial push: straight streaks, no hair.
                NoiseStrength = 0f,
                NoiseFrequency = 3f,
                NoiseScroll = 0.5f,
                Orbit = false,
                OrbitalZ = 0f,
                Radial = 0f,
                Material = new MaterialSpec
                {
                    Name = "SlashP_Spark",
                    Shader = SparkShader,
                    Queue = 3006,
                    Props = new[]
                    {
                        // A glint, under the blade tip's hot spot: in Route A
                        // every spark is laid where the head was and barely
                        // moves, so each is a dot trailing the leading edge,
                        // never on it, and near-white ones read as second and
                        // third accents on the older arc. So every channel
                        // sits under the threshold: only the tip, the rim
                        // behind it and the impact bloom.
                        C("_Color", 0.5f, 0.28f, 0.72f, 1f),
                        C("_CoreColor", 0.85f, 0.68f, 0.95f, 1f),
                        F("_CoreSize", 0.4f),
                        F("_Falloff", 1.6f),
                        F("_Additive", 1f)
                    }
                }
            };

            // ------------------------------------------------------------ Hit impact
            // A separate prefab, SlashImpact, spawned by the VFX manager on a
            // confirmed hit only (the pack's "Hit" cues) - never by SlashEffect,
            // so burst counts here are plain counts, not weights. Orientation:
            // local +Z toward the camera, local +Y along the slash. Placed on
            // the struck body's torso (WeaponVisualizer), never its head. Small
            // - the flash is 0.3 across and shrinks onto the contact - and
            // over before the target has recoiled out from under it, so a hit
            // reads as a sharp accent on the target rather than a second slash
            // or a reticle left hanging in the air. The cut is the one element
            // that gives the direction, so it is the hottest; only it and the
            // flash core bloom. Hierarchy order: Flash (root), Cut, Ring,
            // Sparks, Chips.

            /// <summary>
            /// Every impact system's duration, and the ceiling on the whole
            /// effect: longer than any impact lifetime, so nothing is cut off,
            /// and short enough that a pooled instance frees up quickly.
            /// </summary>
            public const float ImpactDuration = 0.16f;

            /// <summary>
            /// The pack's hit cues: how long the pool keeps an impact (a little
            /// past <see cref="ImpactDuration"/>, so nothing is cut off) and how
            /// much bigger the heavy attack's impact is than the generic one.
            /// Only seed a cue that is missing - the pack is never overwritten,
            /// so once a cue exists it is tuned in the pack itself.
            /// </summary>
            public const float HitCueLifetime = 0.2f;
            public const float HitCueScale = 1f;

            // The finisher lands on a body lying on the floor, about 0.3
            // thick at game distance: at 1.3 the burst was taller than the
            // body and cut its outline in two at the hip for two frames.
            public const float HeavyHitCueScale = 1.1f;

            public static readonly ImpactSpec ImpactFlash = new ImpactSpec
            {
                Name = "Flash",
                // One white-hot pop with a violet edge, gone in under four
                // frames. It collapses onto the contact rather than swelling:
                // a growing additive disc spreads its light over the body it
                // is on as it fades, a shrinking one pins the point of the hit.
                // At 0.45 swelling to 1.3x, with its bloom, it was a disc as
                // wide as the enemy's head, sat on top of it.
                Count = 1,
                Lifetime = new Vector2(0.06f, 0.06f),
                Speed = Vector2.zero,
                Size = new Vector2(0.3f, 0.3f),
                ColorA = Color.white,
                ColorB = Color.white,
                ConeAngle = -1f,
                AlongSwing = false,
                RenderMode = ParticleSystemRenderMode.Billboard,
                SizeOverLife = Line(1f, 0.75f),
                Colors = White(),
                Alphas = Alpha((0f, 1f), (0.3f, 0.55f), (1f, 0f)),
                Material = new MaterialSpec
                {
                    Name = "SlashP_ImpactFlash",
                    Shader = SparkShader,
                    Queue = 3009,
                    Props = new[]
                    {
                        // Only the core blooms; the violet edge sits under
                        // the threshold. At a blue of 1.0 the whole disc
                        // bloomed and its halo, twice the white area, spread
                        // over the target's upper arm up to the chin line.
                        C("_Color", 0.7f, 0.26f, 0.85f, 1f),
                        C("_CoreColor", 2.4f, 2.0f, 2.6f, 1f),
                        F("_CoreSize", 0.4f),
                        F("_Falloff", 1.6f),
                        F("_Additive", 1f)
                    }
                }
            };

            public static readonly ImpactSpec ImpactCut = new ImpactSpec
            {
                Name = "Cut",
                // One streak across the target along the swing: born fast and
                // dragged hard, so it snaps out to about 0.48 long and shrinks
                // as it slows. A stretched particle trails BEHIND its own
                // position, so it is born ahead of the contact: on its first
                // drawn frame the mark spans roughly -0.18..+0.3 across the
                // anchor. Born behind the contact, the whole 0.8 streak lay in
                // the gap between the two fighters and read as a beam joining
                // them; at 0.55 long and 0.15 ahead, a cut leaned down along
                // an overhead swing ran back up from the sternum to the chin.
                Count = 1,
                Lifetime = new Vector2(0.09f, 0.09f),
                Speed = new Vector2(7f, 7f),
                Size = new Vector2(0.07f, 0.07f),
                ColorA = Color.white,
                ColorB = Color.white,
                ConeAngle = 0f,
                ConeRadius = 0.001f,
                AlongSwing = true,
                Offset = new Vector3(0f, 0.2f, 0f),
                RenderMode = ParticleSystemRenderMode.Stretch,
                // Length is about size * LengthScale + speed * VelocityScale:
                // 0.25 + 0.35 at birth, about 0.48 on the first drawn frame.
                VelocityScale = 0.05f,
                LengthScale = 3.5f,
                Drag = 20f,
                SizeOverLife = Line(1f, 0.6f),
                Colors = White(),
                Alphas = Alpha((0f, 1f), (0.5f, 0.9f), (1f, 0f)),
                Material = new MaterialSpec
                {
                    Name = "SlashP_ImpactCut",
                    Shader = SparkShader,
                    Queue = 3010,
                    Props = new[]
                    {
                        // The hottest thing in the impact: the one element
                        // that says which way the blade went through.
                        C("_Color", 1.0f, 0.45f, 1.4f, 1f),
                        C("_CoreColor", 2.8f, 2.3f, 3.0f, 1f),
                        F("_CoreSize", 0.6f),
                        F("_Falloff", 1.2f),
                        F("_Additive", 1f)
                    }
                }
            };

            public static readonly ImpactSpec ImpactRing = new ImpactSpec
            {
                Name = "Ring",
                // A thin violet shock line opening from inside the flash
                // (0.16) to about 0.32 across, gone in three frames - before
                // the target has recoiled out from under it. It is the one
                // part of the impact that draws a closed contour, a round
                // shape that is no part of the body, so it is the smallest
                // and dimmest: at 0.2 to 0.4 over four to five frames it
                // circled the chest and upper arm on light hits and was left
                // on the floor across the hero's shin once a knocked-down
                // body had slid out from under it; at 0.3 to 0.7 over seven
                // it hung where the head had been like a lock-on reticle.
                Count = 1,
                Lifetime = new Vector2(0.05f, 0.05f),
                Speed = Vector2.zero,
                Size = new Vector2(0.16f, 0.16f),
                ColorA = Color.white,
                ColorB = Color.white,
                ConeAngle = -1f,
                AlongSwing = false,
                RenderMode = ParticleSystemRenderMode.Billboard,
                SizeOverLife = EaseOut(1f, 2f),
                Colors = White(),
                Alphas = Alpha((0f, 0.8f), (0.35f, 0.5f), (1f, 0f)),
                Material = new MaterialSpec
                {
                    Name = "SlashP_ImpactRing",
                    Shader = SparkShader,
                    Queue = 3008,
                    Props = new[]
                    {
                        // A ring, so no core: the centre stays empty and the
                        // flash under it is what fills it. Under the bloom
                        // threshold, so it is a line and not a halo.
                        C("_Color", 0.75f, 0.3f, 0.95f, 1f),
                        C("_CoreColor", 0.75f, 0.3f, 0.95f, 1f),
                        F("_CoreSize", 0f),
                        F("_Falloff", 1.2f),
                        F("_Ring", 0.75f),
                        F("_RingWidth", 0.18f),
                        F("_Additive", 1f)
                    }
                }
            };

            public static readonly ImpactSpec ImpactSparks = new ImpactSpec
            {
                Name = "Sparks",
                // A tight, fast fan thrown on along the swing, as if knocked off
                // the edge, backing up the cut's direction and dragged to a
                // stop within about 0.3 of the contact - not a spray over the
                // target's head - and gone in five frames, before a launched
                // or knocked-down target has left them hanging at shin height.
                Count = 5,
                Lifetime = new Vector2(0.05f, 0.09f),
                Speed = new Vector2(5f, 9f),
                Size = new Vector2(0.03f, 0.05f),
                ColorA = new Color(1f, 0.95f, 1f, 1f),
                ColorB = new Color(1f, 0.8f, 0.92f, 1f),
                ConeAngle = 25f,
                ConeRadius = 0.03f,
                AlongSwing = true,
                RenderMode = ParticleSystemRenderMode.Stretch,
                VelocityScale = 0.03f,
                LengthScale = 2f,
                Drag = 12f,
                SizeOverLife = EaseIn(1f, 0f),
                Colors = White(),
                Alphas = Alpha((0f, 1f), (0.6f, 1f), (1f, 0f)),
                Material = new MaterialSpec
                {
                    Name = "SlashP_ImpactSpark",
                    Shader = SparkShader,
                    Queue = 3011,
                    Props = new[]
                    {
                        // Violet-white: a pink edge pushed them towards the
                        // hero's crimson.
                        C("_Color", 1.2f, 0.7f, 1.7f, 1f),
                        C("_CoreColor", 2.2f, 2.0f, 2.6f, 1f),
                        F("_CoreSize", 0.4f),
                        F("_Falloff", 1.6f),
                        F("_Additive", 1f)
                    }
                }
            };

            public static readonly ImpactSpec ImpactChips = new ImpactSpec
            {
                Name = "Chips",
                // Four small turquoise flicks: the brush's identity colour in
                // the impact. Light, not paint - flat teal chips were the
                // enemy's own hue and vanished over it - but in the brush's
                // greener jade and kept under the bloom threshold, so they
                // add no second bright accent. Stretched, gravity off: short
                // dashes thrown along the cut. Fast and wide enough to clear
                // the torso within two or three frames: additive jade over the
                // turquoise body adds next to nothing, so a chip that stays
                // on the body is never seen, and the impact read as all
                // violet-white. Off the body, against the dark floor and
                // backdrop, they are the impact's turquoise. Short-lived, so
                // none is left on the floor once a launched or knocked-down
                // target has moved off the contact.
                Count = 4,
                Lifetime = new Vector2(0.07f, 0.1f),
                Speed = new Vector2(6f, 9f),
                Size = new Vector2(0.03f, 0.045f),
                ColorA = Color.white,
                ColorB = Color.white,
                Gravity = 0f,
                ConeAngle = 65f,
                ConeRadius = 0.05f,
                AlongSwing = true,
                RenderMode = ParticleSystemRenderMode.Stretch,
                // Half the velocity stretch of before at twice the speed:
                // dashes about 0.2 long, not beams.
                VelocityScale = 0.015f,
                LengthScale = 2.5f,
                Drag = 9f,
                SizeOverLife = Line(1f, 0.6f),
                Colors = White(),
                Alphas = Alpha((0f, 1f), (0.6f, 1f), (1f, 0f)),
                Material = new MaterialSpec
                {
                    Name = "SlashP_ImpactChip",
                    Shader = SparkShader,
                    Queue = 3007,
                    Props = new[]
                    {
                        C("_Color", 0.03f, 0.62f, 0.46f, 1f),
                        C("_CoreColor", 0.3f, 0.92f, 0.75f, 1f),
                        F("_CoreSize", 0.35f),
                        F("_Falloff", 1.4f),
                        F("_Additive", 1f)
                    }
                }
            };

            // ------------------------------------------------------------ Route A profile

            // The band's reach along the blade, 0 at the grip and 1 at the tip.
            // It ends at the tip rather than 40% past it: the drawn reach is the
            // real reach, and the thin outer boundary shows where it stops.
            public const float ShapeInnerAlongBlade = 0.5f;
            public const float ShapeOuterAlongBlade = 1.0f;

            // About the world length of the crescent's centre line, so noise and
            // edge patterns tuned on the prefab keep their density on the band.
            public const float ShapeArcLength = 4.5f;

            // The fade: how the remnant leaves once the band has stopped on the
            // damage window's close (StopAfterWindow). It used to be seven
            // frames on a whiff and five on a hit, eroding in a straight line
            // at full opacity - readable, but the cut ended on a hard frame,
            // gone as abruptly as a light switched off. Now about fourteen
            // frames on a whiff, eleven on a hit (ShapeFadeAfterHitSeconds:
            // the impact already says what happened and the target reacts
            // through the band), shaped by ShapeFadeCurve - ease in and out, so
            // the cut settles for a moment before breaking up and trails off
            // instead of stopping dead - with the layers' _FadeOpacity thinning
            // what is still standing as it goes. Still short enough that a
            // step's remnant is gone before the next step's cut is drawn, so a
            // combo does not stack. Longer or shorter here is the one knob for
            // how long the effect stays; the curve and _FadeOpacity are how
            // soft it is on the way out. Route B's layer lifetimes stay longer -
            // the standalone prefab has no hit to compete with.
            public const float ShapeFadeSeconds = 0.24f;
            public const float ShapeFadeAfterHitSeconds = 0.18f;
            public static AnimationCurve ShapeFadeCurve() => AnimationCurve.EaseInOut(0f, 1f, 1f, 0f);
            public const int ShapeBurstCount = 4;

            // With the burst totals above (Sparks 12, Motes 6, Embers and Wisps
            // 5) this lays about 5 sparks, 3 motes and 2 each of embers and
            // wisps on a 4.5-unit swing - fewer still, since the band gates
            // the density by the edge's heat (none through the wind-up, a
            // fifth once a hit has landed, none after the window): sparse, and
            // in game every one of them lives ParticleLifetime, whatever the
            // prefab says - six frames, fading on their own colour-over-life
            // curves, so they die away with the band's softer fade instead of
            // blinking out in under four; the band now outlasts them, so none
            // is left floating past the arc's visible start.
            public const float ParticlesPerUnit = 1.2f;
            public const float ParticleSpeedScale = 0.03f;
            public const float ParticleSpread = 0.25f;
            public const float ParticleSizeScale = 0.8f;
            public const float ParticleOpacity = 1f;
            public const float ParticleLifetime = 0.1f;

            // The hit window (see the readability note above the shape layers).
            // Band laid while the window was shut is drawn at OutsideHitWidth of
            // its window and OutsideHitOpacity of its coverage - with the body
            // anchored outside, a cold line on the outer edge (about 5 px at
            // game distance). HotBeforeWindow is how hot the edge is before the
            // window has ever opened (the wind-up frames) - zero, so the edge
            // and its spray light only where the blade can hit; at a fifth
            // the wind-up laid specks that floated below the arc's visible
            // start and made the cut read as starting lower and earlier than
            // drawn. HotDecaySeconds is how fast the heat dies after the
            // window shuts - five frames: full on the close frame, while the
            // head is still on the blade, then easing off rather than snapping
            // out in one, yet gone before the stopped end can read as detached
            // from the blade the swing carries on with - and
            // HotAfterHit the most it may burn once a hit has landed: a faint
            // lift only, which hands the brightest spot to the impact.
            // StopAfterWindow ends the stroke StopAfterWindowSeconds after the
            // window closes - zero: on the close frame itself, whose pose is
            // still marked inside the window, so the stroke ends on its full
            // head at the blade rather than on a thin exit taper that turned
            // every stopped stroke into a crescent pointed at both ends. The
            // follow-through up to the clip's end cue cannot hit, and drawing
            // it closed the rising cut into a dome round the hero and held
            // every stroke still for six frames after the blade had moved on.
            public const bool UseHitWindow = true;
            public const float OutsideHitWidth = 0.18f;
            public const float OutsideHitOpacity = 0.6f;
            public const float HotDecaySeconds = 0.08f;
            public const float HotBeforeWindow = 0f;
            public const float HotAfterHit = 0.2f;
            public const bool StopAfterWindow = true;
            public const float StopAfterWindowSeconds = 0f;
            public const int Smoothing = 4;
            public const float MinStep = 0.02f;

            // A keyframed swing turns a corner on every key, and a band that
            // follows the tip faithfully draws each one. Half strength irons
            // those out without rounding a hook into a plain arc; two degrees
            // a facet is past where the fastest swing's edge still shows steps.
            public const float ArcSmoothing = 0.5f;
            public const float ArcSegmentDegrees = 2f;

            public const float CircleSpanDegrees = 300f;
            public const float CircleHeadFalloff = 1.2f;
            public const float CircleTailOpacity = 0.5f;
            public const float CircleSquash = 1f;
            public const float CircleWobble = 0f;
            public const float CircleTaper = 0f;
            public const int CircleSegments = 128;
            public const float CircleDepthOffset = 0.06f;
            public const float FollowSeconds = 0.45f;
            public const float FadeSeconds = 0.16f;
        }

        #endregion

        #region Paths and contract

        private const string CrescentFolder = "Assets/GAME/Data/Combat/Visuals/Crescent";
        private const string PrefabFolder = "Assets/GAME/Prefabs/VFX";

        private const string MeshPath = CrescentFolder + "/SlashCrescent_Mesh.asset";
        private const string PrefabPath = PrefabFolder + "/SlashCrescent.prefab";
        private const string ProfilePath = CrescentFolder + "/Slash_Crescent.asset";

        private const string ImpactPrefabPath = PrefabFolder + "/SlashImpact.prefab";
        private const string ImpactRootName = "SlashImpact";

        // The sword's pack, and the hit cues WeaponVisualizer.HandleHit looks
        // up: the attack's own ("Hit." + attack key) first, then the generic.
        private const string PackPath = "Assets/GAME/Data/Combat/Visuals/Sword_VisualPack.asset";
        private const string PackCuesField = "_cues";
        private const string HitCueKey = "Hit";
        private const string HeavyHitCueKey = "Hit.Sword_Heavy";

        private const string MeshName = "SlashCrescent_Mesh";
        private const string EmitterMeshPrefix = "SlashCrescent_Emit_";

        /// <summary>Ribs along a tangential spray's emitter strip.</summary>
        private const int EmitterRibs = 48;

        /// <summary>Keys sampling the eased HIDE ramp.</summary>
        private const int HideKeys = 7;
        private const string RootName = "SlashCrescent";
        private const string ProfileName = "Slash_Crescent";

        private const string LayeredShader = "Game/Slash Layered";
        private const string SparkShader = "Game/Slash Spark";
        private const string LayeredShaderPath = "Assets/GAME/Shaders/SlashLayered.shader";
        private const string SparkShaderPath = "Assets/GAME/Shaders/SlashSpark.shader";

        // Route B (Kind=Prefab, Alignment BladeTrailArc). Not art: these state
        // where the mesh put the blade - tip on radius 1, head at 0 degrees,
        // tail counter-clockwise from it - so the cue has to match the mesh
        // rather than the other way round. Only logged, for whoever sets the cue.
        private const float CueArcRadius = 1f;
        private const float CueArcLeadAngle = 0f;
        private const string CueArcWinding = "TowardPositiveAngle";

        /// <summary>
        /// Far above any speed the sprays reach. Limit Velocity is on only for
        /// its drag, and its speed cap - 1 by default - would otherwise quietly
        /// clamp every spark to a crawl.
        /// </summary>
        private const float NoSpeedLimit = 1000f;

        #endregion

        #region Menu

        [MenuItem("Tools/VFX/Crescent Slash/Build", priority = 0)]
        public static void BuildMenu() => Build(false);

        [MenuItem("Tools/VFX/Crescent Slash/Rebuild And Reset Materials", priority = 1)]
        public static void BuildResetMenu() => Build(true);

        /// <summary>
        /// Entry point for <c>-executeMethod</c>, which cannot pass arguments.
        /// </summary>
        public static void BuildReset() => Build(true);

        #endregion

        #region Build

        /// <summary>
        /// Builds or refreshes every asset of the slash.
        /// </summary>
        /// <param name="resetLooks">
        /// Rewrite the materials and the profile with the <see cref="Look"/>
        /// values even when they already exist. Off, anything the art pass
        /// tuned by hand in the inspector survives the rebuild.
        /// </param>
        public static void Build(bool resetLooks)
        {
            Shader layered = loadShader(LayeredShaderPath, LayeredShader);
            Shader spark = loadShader(SparkShaderPath, SparkShader);

            if (layered == null || spark == null)
                return;

            ensureFolder(CrescentFolder);
            ensureFolder(PrefabFolder);

            Mesh mesh = buildMesh();

            var materials = new Dictionary<MaterialSpec, Material>();
            var materialPaths = new List<string>();

            foreach (MaterialSpec spec in allMaterials())
            {
                Shader shader = spec.Shader == LayeredShader ? layered : spark;
                materials[spec] = ensureMaterial(spec, shader, resetLooks, out string path);
                materialPaths.Add(path);
            }

            GameObject prefab = buildPrefab(mesh, materials);

            if (prefab == null)
                return;

            GameObject impact = buildImpactPrefab(materials, resetLooks);

            List<SlashProfile> profiles = ensureProfiles(prefab, resetLooks);

            // Without the impact prefab there is nothing for a hit cue to
            // spawn, and a cue with no prefab is silently skipped anyway.
            string pack = impact != null ? ensureHitCues(impact) : "skipped (no impact prefab)";

            AssetDatabase.SaveAssets();

            // Colour variants are the slash with nothing changed but colour, so
            // a rebuild of the slash is a rebuild of every variant too - left
            // alone they would keep the old timings, shapes and materials.
            int variants = SlashColorVariantBuilder.RefreshAll();

            var profilePaths = new List<string>(profiles.Count);
            foreach (SlashProfile profile in profiles)
                profilePaths.Add(AssetDatabase.GetAssetPath(profile));

            Debug.Log(
                $"Crescent slash built{(resetLooks ? " (looks reset)" : "")}: " +
                $"mesh '{MeshPath}', materials '{string.Join("', '", materialPaths)}', " +
                $"prefab '{PrefabPath}', impact '{(impact != null ? ImpactPrefabPath : "not saved")}', " +
                $"profiles '{string.Join("', '", profilePaths)}', pack {pack}, " +
                $"colour variants regenerated: {variants}. " +
                $"Route-B cue: ArcRadius {CueArcRadius}, ArcLeadAngle {CueArcLeadAngle}, " +
                $"ArcWinding {CueArcWinding}.",
                profiles.Count > 0 ? profiles[0] : null);
        }

        /// <summary>
        /// Every material: the slash's in hierarchy order, which is also
        /// render-queue order, then the impact's.
        /// </summary>
        private static IEnumerable<MaterialSpec> allMaterials()
        {
            yield return Look.Glow.Material;
            yield return Look.Brush.Material;
            yield return Look.Body.Material;
            yield return Look.Embers.Material;
            yield return Look.Motes.Material;
            yield return Look.Wisps.Material;
            yield return Look.Sparks.Material;

            foreach (ImpactSpec spec in impactSystems())
                yield return spec.Material;
        }

        /// <summary>
        /// The impact's systems in hierarchy order, the first being the root.
        /// </summary>
        private static IEnumerable<ImpactSpec> impactSystems()
        {
            yield return Look.ImpactFlash;
            yield return Look.ImpactCut;
            yield return Look.ImpactRing;
            yield return Look.ImpactSparks;
            yield return Look.ImpactChips;
        }

        /// <summary>
        /// By path first, because a shader that failed to compile is still an
        /// asset at its path but may be missing from <see cref="Shader.Find"/>;
        /// by name as the fallback, so a moved file still resolves.
        /// </summary>
        private static Shader loadShader(string path, string name)
        {
            Shader shader = AssetDatabase.LoadAssetAtPath<Shader>(path);

            if (shader == null)
                shader = Shader.Find(name);

            if (shader == null)
            {
                Debug.LogError(
                    $"Crescent slash: shader '{name}' not found at '{path}' or by name. " +
                    "Nothing was built.");
            }

            return shader;
        }

        /// <summary>
        /// Creates a folder and any missing parents, one level at a time -
        /// <see cref="AssetDatabase.CreateFolder"/> only ever makes one.
        /// </summary>
        private static void ensureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
                return;

            int slash = path.LastIndexOf('/');
            string parent = path.Substring(0, slash);

            ensureFolder(parent);
            AssetDatabase.CreateFolder(parent, path.Substring(slash + 1));
        }

        #endregion

        #region Mesh

        /// <summary>
        /// The crescent every shape layer is drawn on when the prefab plays by
        /// itself (Route B).
        ///
        /// Refilled rather than recreated when it exists, so the prefab's
        /// renderers - and anything else holding the mesh - keep pointing at
        /// the same object.
        /// </summary>
        private static Mesh buildMesh()
        {
            Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(MeshPath);
            bool created = mesh == null;

            if (created)
                mesh = new Mesh();

            fillCrescent(mesh);

            if (created)
                AssetDatabase.CreateAsset(mesh, MeshPath);
            else
                EditorUtility.SetDirty(mesh);

            return mesh;
        }

        /// <summary>
        /// Lays the crescent out in local XY, facing +Z.
        ///
        /// uv0 is (t, s): t runs 0 at the tail to 1 at the head, s 0 on the
        /// inner edge to 1 on the outer - the same (u, v) meaning the band and
        /// ring meshes carry in the game, so one shader draws all three. The
        /// head sits at 0 degrees and the tail 320 degrees counter-clockwise
        /// from it, which is the arc Route B's cue (lead 0, winding toward
        /// positive angle) expects.
        ///
        /// The radius shrinks toward the tail so the stroke spirals inward as
        /// it ages, and the outer rim lifts toward +Z into a shallow bowl, so
        /// seen at an angle the rim catches the eye first and the stroke reads
        /// as having depth rather than as a decal.
        /// </summary>
        private static void fillCrescent(Mesh mesh)
        {
            int along = Look.SegmentsAlong;
            int across = Look.SegmentsAcross;
            int columns = across + 1;
            int count = (along + 1) * columns;

            var vertices = new List<Vector3>(count);
            var uvs = new List<Vector2>(count);
            // Color32, never Color. A mesh particle multiplies its colour by
            // the mesh's, and the particle system reads the mesh's colour as
            // four bytes; List<Color> stores it as four floats, so white came
            // through as the bytes of 1.0f - (0, 0, 0.5, 0.25) - and the life
            // the shader dissolves by started at a quarter.
            var colours = new List<Color32>(count);
            var triangles = new List<int>(along * across * 6);

            for (int i = 0; i <= along; i++)
            {
                float t = i / (float)along;
                float angle = (Look.HeadAngleDegrees + (1f - t) * Look.SpanDegrees) * Mathf.Deg2Rad;
                float eased = t * t * (3f - 2f * t);
                float outer = Look.HeadOuter * Mathf.Lerp(Look.TailRadiusScale, 1f, eased);
                float inner = outer * Look.InnerRatio;
                float cos = Mathf.Cos(angle);
                float sin = Mathf.Sin(angle);

                for (int j = 0; j <= across; j++)
                {
                    float s = j / (float)across;
                    float r = Mathf.Lerp(inner, outer, s);

                    vertices.Add(new Vector3(cos * r, sin * r, Look.Lift * s * s));
                    uvs.Add(new Vector2(t, s));
                    colours.Add(new Color32(255, 255, 255, 255));
                }
            }

            // Wound so the face normal is +Z: t runs clockwise and s outward,
            // and (along, across) in that order is clockwise seen from +Z.
            // Culling is off in both shaders, so this only matters for normals.
            for (int i = 0; i < along; i++)
            {
                for (int j = 0; j < across; j++)
                {
                    int a = i * columns + j;
                    int b = a + columns;
                    int c = a + 1;
                    int d = b + 1;

                    triangles.Add(a);
                    triangles.Add(b);
                    triangles.Add(c);

                    triangles.Add(b);
                    triangles.Add(d);
                    triangles.Add(c);
                }
            }

            mesh.Clear();
            mesh.name = MeshName;
            mesh.indexFormat = IndexFormat.UInt16;
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uvs);
            mesh.SetColors(colours);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
        }

        #endregion

        #region Materials

        /// <summary>
        /// Loads a layer's material, creating it if it is missing.
        ///
        /// A reset writes onto the material that already exists instead of
        /// replacing it. Every property goes back to the shader default first,
        /// so a value the art pass set and <see cref="Look"/> does not list
        /// does not survive a reset by accident, then the listed values go on
        /// top.
        /// </summary>
        private static Material ensureMaterial(
            MaterialSpec spec,
            Shader shader,
            bool reset,
            out string path)
        {
            path = $"{CrescentFolder}/{spec.Name}.mat";

            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);

            if (material == null)
            {
                material = new Material(shader) { name = spec.Name };
                applyMaterial(material, spec);
                AssetDatabase.CreateAsset(material, path);
                return material;
            }

            if (reset || material.shader != shader)
            {
                if (!reset)
                {
                    Debug.LogWarning(
                        $"Crescent slash: '{path}' was on shader '{material.shader.name}', " +
                        $"not '{shader.name}'; its look was reset.",
                        material);
                }

                material.shader = shader;

                var defaults = new Material(shader);
                material.CopyPropertiesFromMaterial(defaults);
                Object.DestroyImmediate(defaults);

                applyMaterial(material, spec);
                EditorUtility.SetDirty(material);
            }

            return material;
        }

        private static void applyMaterial(Material material, MaterialSpec spec)
        {
            for (int i = 0; i < spec.Props.Length; i++)
            {
                Prop prop = spec.Props[i];

                // A name the shader does not have is a typo or a renamed
                // property, and silently setting nothing is how a look drifts.
                if (!material.HasProperty(prop.Name))
                {
                    Debug.LogWarning(
                        $"Crescent slash: '{spec.Name}' has no property '{prop.Name}'.",
                        material);
                    continue;
                }

                switch (prop.Kind)
                {
                    case PropKind.Float:
                        material.SetFloat(prop.Name, prop.Value.x);
                        break;

                    case PropKind.Color:
                        // Look states colours as the shader receives them, and
                        // every colour in the slash shaders is [HDR], which Unity
                        // uploads exactly as stored - it linearises only plain
                        // Colors. So the value goes straight in, and what the
                        // inspector's HDR picker then shows is the same number.
                        // A plain Color would need a gamma round trip here and
                        // would clamp at one in the picker.
                        material.SetColor(
                            prop.Name,
                            new Color(prop.Value.x, prop.Value.y, prop.Value.z, prop.Value.w));
                        break;

                    case PropKind.Vector:
                        material.SetVector(prop.Name, prop.Value);
                        break;
                }
            }

            // Set explicitly because every layer is in the same Transparent
            // queue by its shader, and the ordering between them - glow under
            // brush under body under the spray - is part of the look.
            material.renderQueue = spec.Queue;
        }

        #endregion

        #region Prefab

        /// <summary>
        /// Builds the hierarchy in the scene, saves it over the prefab's own
        /// path and throws the scene copy away.
        ///
        /// Order is part of the contract. <c>SlashEffect</c> takes the shape
        /// layers' materials in hierarchy order and stacks them on one renderer,
        /// so Glow, Brush, Body must come first and in that order; the root
        /// being a system itself means <c>Play()</c> on the prefab's root
        /// cascades to everything under it.
        /// </summary>
        private static GameObject buildPrefab(Mesh mesh, Dictionary<MaterialSpec, Material> materials)
        {
            var root = new GameObject(RootName);

            try
            {
                configureShapeLayer(root.AddComponent<ParticleSystem>(), Look.Glow, mesh, materials);
                configureShapeLayer(addChild(root, Look.Brush.Name), Look.Brush, mesh, materials);
                configureShapeLayer(addChild(root, Look.Body.Name), Look.Body, mesh, materials);
                configureSpray(addChild(root, Look.Embers.Name), Look.Embers, materials);
                configureSpray(addChild(root, Look.Motes.Name), Look.Motes, materials);
                configureSpray(addChild(root, Look.Wisps.Name), Look.Wisps, materials);
                configureSpray(addChild(root, Look.Sparks.Name), Look.Sparks, materials);

                GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath, out bool saved);

                if (!saved || prefab == null)
                {
                    Debug.LogError($"Crescent slash: could not save the prefab to '{PrefabPath}'.");
                    return null;
                }

                return prefab;
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static ParticleSystem addChild(GameObject root, string name)
        {
            var child = new GameObject(name);
            child.transform.SetParent(root.transform, false);
            child.transform.localPosition = Vector3.zero;
            child.transform.localRotation = Quaternion.identity;
            child.transform.localScale = Vector3.one;

            return child.AddComponent<ParticleSystem>();
        }

        /// <summary>
        /// One shape layer: a single mesh particle, the whole crescent, living
        /// exactly as long as its dissolve.
        ///
        /// Its particle colour's alpha is not opacity. The shader reads it as
        /// LIFE, the dissolve threshold, so the alpha curve here is the erosion
        /// timing - the same role <c>_Life</c> plays when the game draws the
        /// band itself. Custom1 carries the rest of the per-particle state: x is
        /// HIDE, the sweep-in from tail to head, y a random seed so repeated
        /// slashes do not share one noise pattern.
        ///
        /// One burst of one particle is also how <c>SlashEffect</c> recognises
        /// this system as a shape rather than spray; more and the game would
        /// trail forty copies of the crescent along the swing.
        /// </summary>
        private static void configureShapeLayer(
            ParticleSystem system,
            ShapeLayerSpec spec,
            Mesh mesh,
            Dictionary<MaterialSpec, Material> materials)
        {
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            resetModules(system);

            ParticleSystem.MainModule main = system.main;
            configureMain(main, Look.Duration);
            main.startLifetime = new ParticleSystem.MinMaxCurve(spec.Lifetime);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0f);
            main.startSize = new ParticleSystem.MinMaxCurve(1f);
            main.startColor = new ParticleSystem.MinMaxGradient(Color.white);
            main.gravityModifier = new ParticleSystem.MinMaxCurve(0f);
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.maxParticles = 1;
            system.useAutoRandomSeed = true;

            ParticleSystem.EmissionModule emission = system.emission;
            emission.enabled = true;
            emission.rateOverTime = new ParticleSystem.MinMaxCurve(0f);
            emission.rateOverDistance = new ParticleSystem.MinMaxCurve(0f);
            emission.SetBursts(new[] { burst(0f, 1) });

            ParticleSystem.ColorOverLifetimeModule colour = system.colorOverLifetime;
            colour.enabled = true;
            colour.color = new ParticleSystem.MinMaxGradient(gradient(White(), spec.Life));

            ParticleSystem.CustomDataModule custom = system.customData;
            custom.enabled = true;
            custom.SetMode(ParticleSystemCustomData.Custom1, ParticleSystemCustomDataMode.Vector);
            custom.SetVectorComponentCount(ParticleSystemCustomData.Custom1, 2);
            custom.SetVector(
                ParticleSystemCustomData.Custom1,
                0,
                new ParticleSystem.MinMaxCurve(1f, hideCurve(spec.Lifetime)));
            custom.SetVector(
                ParticleSystemCustomData.Custom1,
                1,
                new ParticleSystem.MinMaxCurve(0f, 1f));
            custom.SetMode(ParticleSystemCustomData.Custom2, ParticleSystemCustomDataMode.Disabled);

            ParticleSystemRenderer drawn = system.GetComponent<ParticleSystemRenderer>();
            configureRenderer(drawn, materials[spec.Material]);
            drawn.renderMode = ParticleSystemRenderMode.Mesh;
            drawn.mesh = mesh;
            drawn.alignment = ParticleSystemRenderSpace.Local;

            // Instanced mesh particles take a different vertex layout that
            // needs procedural-instancing support in the shader. The slash
            // shader has none and there is one particle per layer anyway, so
            // the plain path is both correct and no slower.
            drawn.enableGPUInstancing = false;

            drawn.SetActiveVertexStreams(new List<ParticleSystemVertexStream>
            {
                ParticleSystemVertexStream.Position,
                ParticleSystemVertexStream.Color,
                ParticleSystemVertexStream.UV,
                ParticleSystemVertexStream.Custom1XY
            });
        }

        /// <summary>
        /// One spray system, emitted from an arc of a circle in the crescent's
        /// own plane.
        ///
        /// The circle's angle runs counter-clockwise from +X, but the slash
        /// sweeps the other way - from the tail at 320 degrees to the head at 0.
        /// Flipping the shape's Y turns the circle's angle phi into -phi, and
        /// rotating it by rho then makes the emission angle rho - phi: an arc
        /// that starts at rho and runs clockwise. A looping arc therefore
        /// follows the head as it sweeps, and a random one covers the region
        /// from rho back toward the head.
        ///
        /// In the game (Route A) the shape and emission are switched off and
        /// every particle is laid along the blade's path instead; the bursts
        /// still matter there, because their total is how each system's share
        /// of that path is weighed.
        /// </summary>
        private static void configureSpray(
            ParticleSystem system,
            SpraySpec spec,
            Dictionary<MaterialSpec, Material> materials)
        {
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            resetModules(system);

            ParticleSystem.MainModule main = system.main;
            configureMain(main, Look.Duration);
            main.startLifetime = new ParticleSystem.MinMaxCurve(spec.Lifetime.x, spec.Lifetime.y);
            main.startSpeed = new ParticleSystem.MinMaxCurve(spec.Speed.x, spec.Speed.y);
            main.startSize = new ParticleSystem.MinMaxCurve(spec.Size.x, spec.Size.y);
            main.startColor = spec.ColorA == spec.ColorB
                ? new ParticleSystem.MinMaxGradient(spec.ColorA)
                : new ParticleSystem.MinMaxGradient(spec.ColorA, spec.ColorB);
            main.gravityModifier = new ParticleSystem.MinMaxCurve(spec.Gravity);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = spec.MaxParticles;
            system.useAutoRandomSeed = true;

            ParticleSystem.EmissionModule emission = system.emission;
            emission.enabled = true;
            emission.rateOverTime = new ParticleSystem.MinMaxCurve(0f);
            emission.rateOverDistance = new ParticleSystem.MinMaxCurve(0f);

            var bursts = new ParticleSystem.Burst[spec.BurstTimes.Length];
            for (int i = 0; i < bursts.Length; i++)
                bursts[i] = burst(spec.BurstTimes[i], spec.BurstCount);
            emission.SetBursts(bursts);

            ParticleSystem.ShapeModule shape = system.shape;
            shape.enabled = true;

            if (spec.TangentEmit)
            {
                // Vertex mode fires along each vertex's normal - the sweep's
                // tangent - and a Loop steps through the vertices one particle
                // at a time, which fillEmitter lays out in emission order. The
                // strip already runs the sweep's way round, so no mirror.
                shape.shapeType = ParticleSystemShapeType.Mesh;
                shape.meshShapeType = ParticleSystemMeshShapeType.Vertex;
                shape.mesh = ensureEmitterMesh(spec);
                shape.meshSpawnMode = spec.ArcMode;
                shape.meshSpawnSpread = 0f;
                shape.meshSpawnSpeed = new ParticleSystem.MinMaxCurve(spec.ArcSpeed);
                shape.useMeshColors = false;
                shape.useMeshMaterialIndex = false;
                shape.normalOffset = 0f;
                shape.scale = Vector3.one;
                shape.rotation = Vector3.zero;
            }
            else
            {
                shape.shapeType = ParticleSystemShapeType.Circle;
                shape.radius = spec.Radius;
                shape.radiusThickness = spec.RadiusThickness;
                shape.arc = spec.Arc;
                shape.arcMode = spec.ArcMode;
                shape.arcSpread = 0f;
                shape.arcSpeed = new ParticleSystem.MinMaxCurve(spec.ArcSpeed);
                shape.scale = new Vector3(1f, -1f, 1f);
                shape.rotation = new Vector3(0f, 0f, spec.ArcStart);
            }

            shape.position = Vector3.zero;
            shape.randomPositionAmount = spec.RandomPosition;
            shape.randomDirectionAmount = spec.RandomDirection;
            shape.sphericalDirectionAmount = 0f;
            shape.alignToDirection = false;

            ParticleSystem.ColorOverLifetimeModule colour = system.colorOverLifetime;
            colour.enabled = true;
            colour.color = new ParticleSystem.MinMaxGradient(gradient(spec.Colors, spec.Alphas));

            ParticleSystem.SizeOverLifetimeModule size = system.sizeOverLifetime;
            size.enabled = true;
            size.separateAxes = false;
            size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(spec.SizeOverLife));

            if (spec.Drag > 0f)
            {
                ParticleSystem.LimitVelocityOverLifetimeModule limit = system.limitVelocityOverLifetime;
                limit.enabled = true;
                limit.separateAxes = false;
                limit.limit = new ParticleSystem.MinMaxCurve(NoSpeedLimit);
                limit.dampen = 0f;
                limit.drag = new ParticleSystem.MinMaxCurve(spec.Drag);
                limit.multiplyDragByParticleSize = false;
                limit.multiplyDragByParticleVelocity = false;
            }

            if (spec.NoiseStrength > 0f)
            {
                ParticleSystem.NoiseModule noise = system.noise;
                noise.enabled = true;
                noise.separateAxes = false;
                noise.strength = new ParticleSystem.MinMaxCurve(spec.NoiseStrength);
                noise.frequency = spec.NoiseFrequency;
                noise.scrollSpeed = new ParticleSystem.MinMaxCurve(spec.NoiseScroll);
                noise.damping = true;
                noise.octaveCount = 1;
                noise.quality = ParticleSystemNoiseQuality.Medium;
                noise.positionAmount = new ParticleSystem.MinMaxCurve(1f);
                noise.rotationAmount = new ParticleSystem.MinMaxCurve(0f);
                noise.sizeAmount = new ParticleSystem.MinMaxCurve(0f);
                noise.remapEnabled = false;
            }

            if (spec.Orbit)
            {
                // x, y and z have to share one curve mode or the module
                // rejects them; the orbital and radial terms are kept constant
                // alongside for the same reason.
                ParticleSystem.VelocityOverLifetimeModule velocity = system.velocityOverLifetime;
                velocity.enabled = true;
                velocity.space = ParticleSystemSimulationSpace.Local;
                velocity.x = new ParticleSystem.MinMaxCurve(0f);
                velocity.y = new ParticleSystem.MinMaxCurve(0f);
                velocity.z = new ParticleSystem.MinMaxCurve(0f);
                velocity.orbitalX = new ParticleSystem.MinMaxCurve(0f);
                velocity.orbitalY = new ParticleSystem.MinMaxCurve(0f);
                velocity.orbitalZ = new ParticleSystem.MinMaxCurve(spec.OrbitalZ);
                velocity.orbitalOffsetX = new ParticleSystem.MinMaxCurve(0f);
                velocity.orbitalOffsetY = new ParticleSystem.MinMaxCurve(0f);
                velocity.orbitalOffsetZ = new ParticleSystem.MinMaxCurve(0f);
                velocity.radial = new ParticleSystem.MinMaxCurve(spec.Radial);
                velocity.speedModifier = new ParticleSystem.MinMaxCurve(1f);
            }

            ParticleSystemRenderer drawn = system.GetComponent<ParticleSystemRenderer>();
            configureRenderer(drawn, materials[spec.Material]);
            drawn.renderMode = spec.RenderMode;
            drawn.alignment = ParticleSystemRenderSpace.View;
            drawn.cameraVelocityScale = 0f;

            if (spec.RenderMode == ParticleSystemRenderMode.Stretch)
            {
                drawn.velocityScale = spec.VelocityScale;
                drawn.lengthScale = spec.LengthScale;
            }

            drawn.SetActiveVertexStreams(new List<ParticleSystemVertexStream>
            {
                ParticleSystemVertexStream.Position,
                ParticleSystemVertexStream.Color,
                ParticleSystemVertexStream.UV
            });
        }

        /// <summary>
        /// What every system shares. A one-shot, so no loop and a short
        /// duration; always simulated, because a 0.6 s slash that culling
        /// paused and resumed off its bounds would pop rather than play. The
        /// duration is the caller's, because the impact is a much shorter
        /// one-shot than the slash, and the VFX manager derives a pooled
        /// instance's default lifetime from it.
        /// </summary>
        private static void configureMain(ParticleSystem.MainModule main, float duration)
        {
            main.duration = duration;
            main.loop = false;
            main.prewarm = false;
            main.playOnAwake = true;
            main.startDelay = new ParticleSystem.MinMaxCurve(0f);
            main.startSize3D = false;
            main.startRotation3D = false;
            main.startRotation = new ParticleSystem.MinMaxCurve(0f);
            main.flipRotation = 0f;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            main.simulationSpeed = 1f;
            main.useUnscaledTime = false;
            main.stopAction = ParticleSystemStopAction.None;
            main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
        }

        /// <summary>
        /// Switches off every optional module, so what each system uses is
        /// exactly what its configure step turns back on. AddComponent hands
        /// back a system with a Cone shape already on; that and anything the
        /// defaults might grow in a later Unity should not leak into the look.
        /// </summary>
        private static void resetModules(ParticleSystem system)
        {
            ParticleSystem.ShapeModule shape = system.shape;
            shape.enabled = false;
            ParticleSystem.VelocityOverLifetimeModule velocity = system.velocityOverLifetime;
            velocity.enabled = false;
            ParticleSystem.LimitVelocityOverLifetimeModule limit = system.limitVelocityOverLifetime;
            limit.enabled = false;
            ParticleSystem.InheritVelocityModule inherit = system.inheritVelocity;
            inherit.enabled = false;
            ParticleSystem.LifetimeByEmitterSpeedModule lifetimeBySpeed = system.lifetimeByEmitterSpeed;
            lifetimeBySpeed.enabled = false;
            ParticleSystem.ForceOverLifetimeModule force = system.forceOverLifetime;
            force.enabled = false;
            ParticleSystem.ColorOverLifetimeModule colour = system.colorOverLifetime;
            colour.enabled = false;
            ParticleSystem.ColorBySpeedModule colourBySpeed = system.colorBySpeed;
            colourBySpeed.enabled = false;
            ParticleSystem.SizeOverLifetimeModule size = system.sizeOverLifetime;
            size.enabled = false;
            ParticleSystem.SizeBySpeedModule sizeBySpeed = system.sizeBySpeed;
            sizeBySpeed.enabled = false;
            ParticleSystem.RotationOverLifetimeModule rotation = system.rotationOverLifetime;
            rotation.enabled = false;
            ParticleSystem.RotationBySpeedModule rotationBySpeed = system.rotationBySpeed;
            rotationBySpeed.enabled = false;
            ParticleSystem.ExternalForcesModule external = system.externalForces;
            external.enabled = false;
            ParticleSystem.NoiseModule noise = system.noise;
            noise.enabled = false;
            ParticleSystem.CollisionModule collision = system.collision;
            collision.enabled = false;
            ParticleSystem.TriggerModule trigger = system.trigger;
            trigger.enabled = false;
            ParticleSystem.SubEmittersModule subEmitters = system.subEmitters;
            subEmitters.enabled = false;
            ParticleSystem.TextureSheetAnimationModule sheet = system.textureSheetAnimation;
            sheet.enabled = false;
            ParticleSystem.LightsModule lights = system.lights;
            lights.enabled = false;
            ParticleSystem.TrailModule trails = system.trails;
            trails.enabled = false;
            ParticleSystem.CustomDataModule custom = system.customData;
            custom.enabled = false;
        }

        /// <summary>
        /// Unlit, sorted by render queue alone, and kept out of every lighting
        /// system: the slash is light, not something lit, and a shadow or a
        /// probe sample on it would only cost.
        /// </summary>
        private static void configureRenderer(ParticleSystemRenderer drawn, Material material)
        {
            drawn.sharedMaterial = material;
            drawn.trailMaterial = null;
            drawn.sortMode = ParticleSystemSortMode.None;
            drawn.shadowCastingMode = ShadowCastingMode.Off;
            drawn.receiveShadows = false;
            drawn.lightProbeUsage = LightProbeUsage.Off;
            drawn.reflectionProbeUsage = ReflectionProbeUsage.Off;
        }

        private static ParticleSystem.Burst burst(float time, int count)
        {
            var shot = new ParticleSystem.Burst(time, new ParticleSystem.MinMaxCurve(count));
            shot.cycleCount = 1;
            shot.probability = 1f;
            return shot;
        }

        /// <summary>
        /// HIDE over a shape layer's normalised lifetime: 1 → 0 by
        /// <see cref="Look.RevealSeconds"/>, fast at first and settling onto 0,
        /// so the sweep snaps out of the tail and eases into the head. The key
        /// time is a fraction of this layer's own lifetime, which is why each
        /// layer gets its own curve.
        /// </summary>
        private static AnimationCurve hideCurve(float lifetime)
        {
            float end = Mathf.Clamp(Look.RevealSeconds / Mathf.Max(lifetime, 1e-3f), 1e-3f, 0.999f);
            float start = Mathf.Clamp01(Look.RevealStartHide);
            float ease = Mathf.Max(Look.RevealEase, 1f);

            // start * q^n with q = 1 - f, sampled at several keys with the
            // curve's own tangents, so the Hermite segments cannot bow away
            // from it (for n = 2 they reproduce it exactly). revealHeadT is the
            // same function, which keeps every Loop emitter vertex on the head.
            // The last key has q = 0, so its slope is 0 for any n >= 1.
            var keys = new Keyframe[HideKeys + 1];
            for (int i = 0; i < HideKeys; i++)
            {
                float f = i / (float)(HideKeys - 1);
                float q = 1f - f;
                float value = start * Mathf.Pow(q, ease);
                float slope = -ease * start * Mathf.Pow(q, ease - 1f) / end;
                keys[i] = new Keyframe(f * end, value, slope, i == HideKeys - 1 ? 0f : slope);
            }

            keys[HideKeys] = new Keyframe(1f, 0f, 0f, 0f);
            return new AnimationCurve(keys);
        }

        /// <summary>
        /// The strip a tangential spray emits from, refilled in place like the
        /// crescent so the prefab keeps pointing at the same object. Kept
        /// readable - the shape module samples its vertices on the CPU.
        /// </summary>
        private static Mesh ensureEmitterMesh(SpraySpec spec)
        {
            string path = $"{CrescentFolder}/{EmitterMeshPrefix}{spec.Name}.asset";
            Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            bool created = mesh == null;

            if (created)
                mesh = new Mesh();

            fillEmitter(mesh, spec);

            if (created)
                AssetDatabase.CreateAsset(mesh, path);
            else
                EditorUtility.SetDirty(mesh);

            return mesh;
        }

        /// <summary>
        /// The strip's vertices, whose normals are the directions the spray
        /// fires along.
        ///
        /// A Random strip is ribs in t order across <see cref="SpraySpec.EmitT"/>,
        /// two vertices each. A Loop strip is different, because a Vertex-mode
        /// Loop does not walk the mesh in time: it steps one vertex per particle
        /// emitted and ignores the spawn speed (measured in the lab - 24 sparks
        /// took the first 24 vertices, a quarter of a 98-vertex strip, whatever
        /// the speed). So a Loop strip is laid in emission order instead, one
        /// vertex per particle, each where the head is when its burst fires;
        /// the loop then lands on the head burst by burst, and a burst's
        /// particles fill the arc the head covered since the one before.
        /// </summary>
        private static void fillEmitter(Mesh mesh, SpraySpec spec)
        {
            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var triangles = new List<int>();

            if (spec.ArcMode == ParticleSystemShapeMultiModeValue.Loop)
            {
                int bursts = spec.BurstTimes.Length;
                int per = Mathf.Max(spec.BurstCount, 1);

                for (int b = 0; b < bursts; b++)
                {
                    // Where the head is drawn, not where the curve has it.
                    float head = revealHeadT(spec.BurstTimes[b] - Look.HeadSampleLag);

                    // The first burst has no predecessor; it borrows the gap to
                    // the next one so its particles spread the same way.
                    float gap = b > 0
                        ? head - revealHeadT(spec.BurstTimes[b - 1] - Look.HeadSampleLag)
                        : bursts > 1 ? revealHeadT(spec.BurstTimes[1] - Look.HeadSampleLag) - head : 0f;
                    gap = Mathf.Max(gap, 0f);

                    for (int j = 0; j < per; j++)
                    {
                        float t = head - spec.EmitLag - gap * (j + 0.5f) / per;

                        // Alternate sides of the strip, shifted per burst, so
                        // consecutive particles do not stack on one line.
                        float across = per > 1
                            ? Mathf.Lerp(-1f, 1f, ((j + b) % per) / (float)(per - 1))
                            : 0f;

                        addEmitterVertex(vertices, normals, spec, t, across);
                    }
                }

                // Vertex mode never reads the triangles, but a mesh with none
                // is not one the shape module accepts.
                for (int k = 0; k + 2 < vertices.Count; k++)
                {
                    triangles.Add(k);
                    triangles.Add(k + 1);
                    triangles.Add(k + 2);
                }
            }
            else
            {
                for (int i = 0; i <= EmitterRibs; i++)
                {
                    float t = Mathf.Lerp(spec.EmitT.x, spec.EmitT.y, i / (float)EmitterRibs);
                    addEmitterVertex(vertices, normals, spec, t, -1f);
                    addEmitterVertex(vertices, normals, spec, t, 1f);
                }

                for (int i = 0; i < EmitterRibs; i++)
                {
                    int a = i * 2;
                    triangles.Add(a);
                    triangles.Add(a + 2);
                    triangles.Add(a + 1);
                    triangles.Add(a + 1);
                    triangles.Add(a + 2);
                    triangles.Add(a + 3);
                }
            }

            mesh.Clear();
            mesh.name = EmitterMeshPrefix + spec.Name;
            mesh.indexFormat = IndexFormat.UInt16;
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
        }

        /// <summary>
        /// One emitter vertex at crescent <paramref name="t"/>, <paramref name="across"/>
        /// in -1..1 of the strip's half-width. Its normal is the direction the
        /// head travels there - clockwise, from 320 degrees towards 0 - tipped
        /// outward by <see cref="SpraySpec.EmitOutwardDegrees"/>.
        /// </summary>
        private static void addEmitterVertex(
            List<Vector3> vertices,
            List<Vector3> normals,
            SpraySpec spec,
            float t,
            float across)
        {
            float angle = (Look.HeadAngleDegrees + (1f - t) * Look.SpanDegrees) * Mathf.Deg2Rad;
            float clamped = Mathf.Clamp01(t);
            float eased = clamped * clamped * (3f - 2f * clamped);
            float outer = Look.HeadOuter * Mathf.Lerp(Look.TailRadiusScale, 1f, eased);
            float cos = Mathf.Cos(angle);
            float sin = Mathf.Sin(angle);

            float bend = spec.EmitOutwardDegrees * Mathf.Deg2Rad;
            var travel = new Vector3(sin, -cos, 0f);
            var outward = new Vector3(cos, sin, 0f);

            float r = outer * spec.EmitRadius + across * spec.EmitJitter;
            float s = Mathf.Clamp01((r / Mathf.Max(outer, 1e-4f) - Look.InnerRatio)
                / Mathf.Max(1f - Look.InnerRatio, 1e-3f));

            vertices.Add(new Vector3(cos * r, sin * r, Look.Lift * s * s));
            normals.Add((Mathf.Cos(bend) * travel + Mathf.Sin(bend) * outward).normalized);
        }

        /// <summary>
        /// The head's crescent t at <paramref name="time"/> seconds - 1 minus
        /// the eased HIDE ramp <see cref="hideCurve"/> lays down.
        /// </summary>
        private static float revealHeadT(float time)
        {
            float progress = Mathf.Clamp01(time / Mathf.Max(Look.RevealSeconds, 1e-4f));
            float q = 1f - progress;
            return 1f - Look.RevealStartHide * Mathf.Pow(q, Mathf.Max(Look.RevealEase, 1f));
        }

        private static Gradient gradient(GradientColorKey[] colours, GradientAlphaKey[] alphas)
        {
            var result = new Gradient();
            result.SetKeys(colours, alphas);
            return result;
        }

        #endregion

        #region Impact

        /// <summary>
        /// Builds the hit impact the same way as the slash - in the scene,
        /// saved over its own path so its GUID and every cue holding it
        /// survive, then the scene copy thrown away.
        ///
        /// A separate prefab rather than another system under the slash,
        /// because the two answer different questions. The slash says where
        /// the blade went and plays on every swing; the impact says that it
        /// connected, and must play on a hit and never on a whiff. Only the
        /// hit scan knows which, so the impact is spawned from there, through
        /// the VFX manager's pool, where the slash could never reach it.
        ///
        /// The root is the flash, so the manager's <c>Play(true)</c> on the
        /// root cascades to everything under it; every system is a
        /// non-looping one-shot with its bursts at 0, which is what a pooled
        /// instance replayed from the start needs.
        ///
        /// The root also carries the impact's tuning - intensity, duration
        /// and fade - over the systems as authored here. Those are the knobs
        /// an art pass turns on the prefab, so a plain rebuild carries them
        /// over from the prefab it replaces; only a reset puts them back to
        /// the authored look.
        /// </summary>
        private static GameObject buildImpactPrefab(Dictionary<MaterialSpec, Material> materials, bool resetLooks)
        {
            string tuned = null;

            if (!resetLooks)
            {
                var existing = AssetDatabase.LoadAssetAtPath<GameObject>(ImpactPrefabPath);
                if (existing != null && existing.TryGetComponent(out Combat.ImpactTuning previous))
                    tuned = EditorJsonUtility.ToJson(previous);
            }

            var root = new GameObject(ImpactRootName);

            try
            {
                var tuning = root.AddComponent<Combat.ImpactTuning>();
                if (tuned != null)
                    EditorJsonUtility.FromJsonOverwrite(tuned, tuning);

                bool first = true;

                foreach (ImpactSpec spec in impactSystems())
                {
                    ParticleSystem system = first
                        ? root.AddComponent<ParticleSystem>()
                        : addChild(root, spec.Name);

                    first = false;

                    // Placed and turned before the system is configured: the
                    // turn is what points the emission along the swing.
                    if (system.gameObject != root)
                    {
                        system.transform.localPosition = spec.Offset;
                        system.transform.localRotation = spec.AlongSwing
                            ? SwingAxisRotation
                            : Quaternion.identity;
                    }

                    configureImpact(system, spec, materials);
                }

                GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, ImpactPrefabPath, out bool saved);

                if (!saved || prefab == null)
                {
                    Debug.LogError($"Crescent slash: could not save the impact prefab to '{ImpactPrefabPath}'.");
                    return null;
                }

                return prefab;
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        /// <summary>
        /// Turns a child's +Z - the direction a cone, and a system with no
        /// shape, emits along - onto the parent's +Y, the slash direction.
        /// A rotation of -90 degrees about X carries (0, 0, 1) to (0, 1, 0).
        /// </summary>
        private static readonly Quaternion SwingAxisRotation = Quaternion.Euler(-90f, 0f, 0f);

        /// <summary>
        /// One impact system. Everything is stated, as for the slash's own
        /// systems, and nothing is left to SlashEffect's Route A: the manager
        /// plays this prefab as authored.
        ///
        /// Particles that move simulate in world space, so a spark thrown
        /// along the swing stays where it was thrown whatever the instance
        /// does next; the flash and ring do not move, and stay local.
        /// </summary>
        private static void configureImpact(
            ParticleSystem system,
            ImpactSpec spec,
            Dictionary<MaterialSpec, Material> materials)
        {
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            resetModules(system);

            bool moves = spec.Speed.y > 0f || spec.Gravity != 0f;

            ParticleSystem.MainModule main = system.main;
            configureMain(main, Look.ImpactDuration);
            main.startLifetime = range(spec.Lifetime);
            main.startSpeed = range(spec.Speed);
            main.startSize = range(spec.Size);
            main.startColor = spec.ColorA == spec.ColorB
                ? new ParticleSystem.MinMaxGradient(spec.ColorA)
                : new ParticleSystem.MinMaxGradient(spec.ColorA, spec.ColorB);
            main.gravityModifier = new ParticleSystem.MinMaxCurve(spec.Gravity);
            main.simulationSpace = moves
                ? ParticleSystemSimulationSpace.World
                : ParticleSystemSimulationSpace.Local;
            main.maxParticles = Mathf.Max(spec.Count, 1);
            system.useAutoRandomSeed = true;

            ParticleSystem.EmissionModule emission = system.emission;
            emission.enabled = true;
            emission.rateOverTime = new ParticleSystem.MinMaxCurve(0f);
            emission.rateOverDistance = new ParticleSystem.MinMaxCurve(0f);
            emission.SetBursts(new[] { burst(0f, Mathf.Max(spec.Count, 1)) });

            // A negative angle leaves the shape off: the particle is born on
            // the system's origin, which is all a flash or a ring needs.
            if (spec.ConeAngle >= 0f)
            {
                ParticleSystem.ShapeModule shape = system.shape;
                shape.enabled = true;
                shape.shapeType = ParticleSystemShapeType.Cone;
                shape.angle = spec.ConeAngle;
                shape.radius = Mathf.Max(spec.ConeRadius, 1e-4f);
                shape.radiusThickness = 1f;
                shape.arc = 360f;
                shape.arcMode = ParticleSystemShapeMultiModeValue.Random;
                shape.arcSpread = 0f;
                shape.position = Vector3.zero;
                shape.rotation = Vector3.zero;
                shape.scale = Vector3.one;
                shape.randomPositionAmount = 0f;
                shape.randomDirectionAmount = 0f;
                shape.sphericalDirectionAmount = 0f;
                shape.alignToDirection = false;
            }

            ParticleSystem.ColorOverLifetimeModule colour = system.colorOverLifetime;
            colour.enabled = true;
            colour.color = new ParticleSystem.MinMaxGradient(gradient(spec.Colors, spec.Alphas));

            ParticleSystem.SizeOverLifetimeModule size = system.sizeOverLifetime;
            size.enabled = true;
            size.separateAxes = false;
            size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(spec.SizeOverLife));

            if (spec.Drag > 0f)
            {
                ParticleSystem.LimitVelocityOverLifetimeModule limit = system.limitVelocityOverLifetime;
                limit.enabled = true;
                limit.separateAxes = false;
                limit.limit = new ParticleSystem.MinMaxCurve(NoSpeedLimit);
                limit.dampen = 0f;
                limit.drag = new ParticleSystem.MinMaxCurve(spec.Drag);
                limit.multiplyDragByParticleSize = false;
                limit.multiplyDragByParticleVelocity = false;
            }

            ParticleSystemRenderer drawn = system.GetComponent<ParticleSystemRenderer>();
            configureRenderer(drawn, materials[spec.Material]);
            drawn.renderMode = spec.RenderMode;
            drawn.alignment = ParticleSystemRenderSpace.View;
            drawn.cameraVelocityScale = 0f;

            if (spec.RenderMode == ParticleSystemRenderMode.Stretch)
            {
                drawn.velocityScale = spec.VelocityScale;
                drawn.lengthScale = spec.LengthScale;
            }

            drawn.SetActiveVertexStreams(new List<ParticleSystemVertexStream>
            {
                ParticleSystemVertexStream.Position,
                ParticleSystemVertexStream.Color,
                ParticleSystemVertexStream.UV
            });
        }

        /// <summary>A constant when both ends agree, otherwise random between them.</summary>
        private static ParticleSystem.MinMaxCurve range(Vector2 minMax) =>
            Mathf.Approximately(minMax.x, minMax.y)
                ? new ParticleSystem.MinMaxCurve(minMax.x)
                : new ParticleSystem.MinMaxCurve(minMax.x, minMax.y);

        #endregion

        #region Pack

        /// <summary>
        /// Makes sure the sword's pack has the hit cues the impact is spawned
        /// by, adding each one that is missing at the end of the list.
        ///
        /// Only ever appends. An existing cue is left exactly as it is, even a
        /// "Hit" pointing at some other prefab: that is a choice someone made
        /// in the pack, and the pack, not this builder, is where the weapon's
        /// look is decided. Existing cues are never reordered either, because
        /// the pack keeps the first of any duplicated key.
        ///
        /// Returns a short account for the build log.
        /// </summary>
        private static string ensureHitCues(GameObject impact)
        {
            var pack = AssetDatabase.LoadAssetAtPath<WeaponVisualPack>(PackPath);

            if (pack == null)
            {
                Debug.LogWarning(
                    $"Crescent slash: no weapon visual pack at '{PackPath}'; the hit cues were not added. " +
                    $"Add a '{HitCueKey}' cue (Kind Prefab, Prefab '{ImpactPrefabPath}') by hand.");
                return "not found";
            }

            var serialized = new SerializedObject(pack);
            SerializedProperty cues = serialized.FindProperty(PackCuesField);

            if (cues == null || !cues.isArray)
            {
                Debug.LogError(
                    $"Crescent slash: '{PackPath}' has no '{PackCuesField}' list; the hit cues were not added.",
                    pack);
                return "unreadable";
            }

            var added = new List<string>();
            addHitCue(cues, HitCueKey, impact, Look.HitCueScale, added);
            addHitCue(cues, HeavyHitCueKey, impact, Look.HeavyHitCueScale, added);

            if (added.Count == 0)
                return $"'{PackPath}' already had its hit cues";

            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(pack);

            Debug.Log($"Crescent slash: added cue(s) '{string.Join("', '", added)}' to '{PackPath}'.", pack);
            return $"'{PackPath}' +{string.Join(", +", added)}";
        }

        /// <summary>
        /// Appends one hit cue unless the list already has that key (the
        /// pack's own comparison: trimmed, case-insensitive).
        ///
        /// Written as a whole entry through <c>boxedValue</c>, starting from
        /// <see cref="VisualCueEntry.Default"/>. Growing the array copies the
        /// last element into the new slot, so setting only the fields a hit
        /// cares about would leave the rest - a slash profile, an anchor - as
        /// whatever the last cue happened to be.
        /// </summary>
        private static void addHitCue(
            SerializedProperty cues,
            string key,
            GameObject impact,
            float scale,
            List<string> added)
        {
            for (int i = 0; i < cues.arraySize; i++)
            {
                SerializedProperty existing = cues.GetArrayElementAtIndex(i).FindPropertyRelative(nameof(VisualCueEntry.CueKey));

                if (existing != null && string.Equals(
                        existing.stringValue?.Trim(),
                        key,
                        System.StringComparison.OrdinalIgnoreCase))
                    return;
            }

            VisualCueEntry entry = VisualCueEntry.Default;
            entry.CueKey = key;
            entry.Kind = VisualCueKind.Prefab;
            entry.Prefab = impact;
            entry.SlashProfile = null;
            entry.Anchor = VisualAnchor.BladeTip;
            entry.Scale = Vector3.one * scale;
            entry.ScaleBy = VisualScalar.Fixed;
            entry.RateBy = VisualScalar.Fixed;
            entry.Lifetime = Look.HitCueLifetime;

            int index = cues.arraySize;
            cues.arraySize = index + 1;
            cues.GetArrayElementAtIndex(index).boxedValue = entry;
            added.Add(key);
        }

        #endregion

        #region Profile

        /// <summary>
        /// Every Route A profile of this slash. One is created at
        /// <see cref="ProfilePath"/> only when none exists; each existing one
        /// is reset onto the same object when asked, so every cue holding it
        /// keeps its reference. All are pointed at the prefab, which is what
        /// makes Route A draw this slash at all.
        ///
        /// Every one, not the first found: a duplicated profile keeps drawing
        /// this prefab, and a reset that reached only one of two left the one
        /// the pack actually used - "Slash_Crescent 1" - on the old look while
        /// the unused copy was updated.
        /// </summary>
        private static List<SlashProfile> ensureProfiles(GameObject prefab, bool reset)
        {
            List<SlashProfile> profiles = findProfiles();

            if (profiles.Count == 0)
            {
                var profile = ScriptableObject.CreateInstance<SlashProfile>();
                profile.name = ProfileName;
                applyProfile(profile);
                profile.Particles = prefab;
                AssetDatabase.CreateAsset(profile, ProfilePath);
                Debug.Log($"Crescent slash: created profile '{ProfilePath}'.", profile);
                profiles.Add(profile);
                return profiles;
            }

            foreach (SlashProfile profile in profiles)
            {
                if (reset)
                {
                    // CopySerialized copies the name too, and an asset whose
                    // object name differs from its file name is one Unity
                    // warns about on every import - so each keeps its own.
                    string name = profile.name;

                    // Back to the class defaults first, so fields this builder
                    // does not set end up where a new profile would have them.
                    var fresh = ScriptableObject.CreateInstance<SlashProfile>();
                    EditorUtility.CopySerialized(fresh, profile);
                    Object.DestroyImmediate(fresh);

                    profile.name = name;
                    applyProfile(profile);
                }

                profile.Particles = prefab;
                EditorUtility.SetDirty(profile);

                Debug.Log(
                    $"Crescent slash: {(reset ? "reset" : "pointed at the prefab")} profile " +
                    $"'{AssetDatabase.GetAssetPath(profile)}'.",
                    profile);
            }

            return profiles;
        }

        /// <summary>
        /// Every profile that is this slash's: each whose particles are the
        /// crescent prefab, plus the one at <see cref="ProfilePath"/> or a
        /// moved one by its name (<see cref="findMovedProfile"/>) even if its
        /// prefab slot was cleared. Sorted by path, so the log reads the same
        /// on every build.
        /// </summary>
        private static List<SlashProfile> findProfiles()
        {
            var found = new List<SlashProfile>();

            void add(SlashProfile candidate)
            {
                if (candidate != null && !found.Contains(candidate))
                    found.Add(candidate);
            }

            add(AssetDatabase.LoadAssetAtPath<SlashProfile>(ProfilePath));
            add(findMovedProfile());

            // By path, not by reference: the prefab was just saved over, and
            // the path is what does not depend on which load of it is held.
            foreach (string guid in AssetDatabase.FindAssets($"t:{nameof(SlashProfile)}"))
            {
                var candidate = AssetDatabase.LoadAssetAtPath<SlashProfile>(AssetDatabase.GUIDToAssetPath(guid));

                if (candidate != null
                    && candidate.Particles != null
                    && AssetDatabase.GetAssetPath(candidate.Particles) == PrefabPath)
                    add(candidate);
            }

            found.Sort((a, b) => string.CompareOrdinal(
                AssetDatabase.GetAssetPath(a),
                AssetDatabase.GetAssetPath(b)));

            return found;
        }

        /// <summary>
        /// The profile wherever it was moved to. Cues hold it by reference, so
        /// a profile filed away next to the others is still this slash's, and
        /// creating a fresh one at the original path would split the look
        /// across two assets with only one of them in use.
        /// </summary>
        private static SlashProfile findMovedProfile()
        {
            foreach (string guid in AssetDatabase.FindAssets($"{ProfileName} t:{nameof(SlashProfile)}"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);

                if (System.IO.Path.GetFileNameWithoutExtension(path) == ProfileName)
                    return AssetDatabase.LoadAssetAtPath<SlashProfile>(path);
            }

            return null;
        }

        private static void applyProfile(SlashProfile profile)
        {
            profile.DrawRibbon = false;
            profile.Material = null;
            profile.ShapeMode = SlashShapeMode.Band;
            profile.ShapeInnerAlongBlade = Look.ShapeInnerAlongBlade;
            profile.ShapeOuterAlongBlade = Look.ShapeOuterAlongBlade;
            profile.ShapeArcLength = Look.ShapeArcLength;
            profile.ShapeFadeSeconds = Look.ShapeFadeSeconds;
            profile.ShapeFadeAfterHitSeconds = Look.ShapeFadeAfterHitSeconds;
            profile.ShapeFadeCurve = Look.ShapeFadeCurve();
            profile.ShapeBurstCount = Look.ShapeBurstCount;
            profile.UseHitWindow = Look.UseHitWindow;
            profile.OutsideHitWidth = Look.OutsideHitWidth;
            profile.OutsideHitOpacity = Look.OutsideHitOpacity;
            profile.HotDecaySeconds = Look.HotDecaySeconds;
            profile.HotBeforeWindow = Look.HotBeforeWindow;
            profile.HotAfterHit = Look.HotAfterHit;
            profile.StopAfterWindow = Look.StopAfterWindow;
            profile.StopAfterWindowSeconds = Look.StopAfterWindowSeconds;
            profile.ParticlesPerUnit = Look.ParticlesPerUnit;
            profile.ParticleSpeedScale = Look.ParticleSpeedScale;
            profile.ParticleSpread = Look.ParticleSpread;
            profile.ParticleSizeScale = Look.ParticleSizeScale;
            profile.ParticleOpacity = Look.ParticleOpacity;
            profile.ParticleLifetime = Look.ParticleLifetime;
            profile.AlignParticlesToSweep = true;
            profile.Smoothing = Look.Smoothing;
            profile.MinStep = Look.MinStep;
            profile.ArcSmoothing = Look.ArcSmoothing;
            profile.ArcSegmentDegrees = Look.ArcSegmentDegrees;

            // Circle mode is not this slash's default, but a cue that switches
            // to it should still draw correctly: one texture frame, because the
            // layered shader expects u in [0, 1] around the ring.
            profile.CircleMaterial = null;
            profile.CircleSpanDegrees = Look.CircleSpanDegrees;
            profile.CircleRadiusSource = SlashCircleRadius.Widest;
            profile.CircleFollowSweep = true;
            profile.CircleHeadFalloff = Look.CircleHeadFalloff;
            profile.CircleTailOpacity = Look.CircleTailOpacity;
            profile.CircleSquash = Look.CircleSquash;
            profile.CircleWobble = Look.CircleWobble;
            profile.CircleTaper = Look.CircleTaper;
            profile.CircleSegments = Look.CircleSegments;
            profile.CircleDepthOffset = Look.CircleDepthOffset;
            profile.CircleTextureFrames = 1;
            profile.CircleTextureFrame = 0;
            profile.CircleTextureFlip = false;

            profile.AutoStopWhenStill = true;
            profile.FollowSeconds = Look.FollowSeconds;
            profile.FadeSeconds = Look.FadeSeconds;
        }

        #endregion

        #region Spec types and helpers

        private enum PropKind : byte
        {
            Float,
            Color,
            Vector
        }

        /// <summary>One material property and the value it is set to.</summary>
        private readonly struct Prop
        {
            public readonly string Name;
            public readonly PropKind Kind;
            public readonly Vector4 Value;

            public Prop(string name, PropKind kind, Vector4 value)
            {
                Name = name;
                Kind = kind;
                Value = value;
            }
        }

        private sealed class MaterialSpec
        {
            public string Name;
            public string Shader;
            public int Queue;
            public Prop[] Props;
        }

        private sealed class ShapeLayerSpec
        {
            public string Name;

            /// <summary>Seconds. Also the length of the dissolve, since LIFE rides the particle's age.</summary>
            public float Lifetime;

            /// <summary>The LIFE curve: particle alpha over normalised lifetime, read as the dissolve threshold.</summary>
            public GradientAlphaKey[] Life;

            public MaterialSpec Material;
        }

        private sealed class SpraySpec
        {
            public string Name;
            public MaterialSpec Material;

            public float[] BurstTimes;
            public int BurstCount;

            /// <summary>(min, max) seconds.</summary>
            public Vector2 Lifetime;

            /// <summary>(min, max) units per second.</summary>
            public Vector2 Speed;

            /// <summary>(min, max) units.</summary>
            public Vector2 Size;

            /// <summary>Start colour; two different values pick randomly between them.</summary>
            public Color ColorA;
            public Color ColorB;

            public float Gravity;
            public int MaxParticles;

            public float Radius;
            public float RadiusThickness;

            /// <summary>Degrees of arc emitted from, running clockwise from <see cref="ArcStart"/>.</summary>
            public float Arc;

            /// <summary>Rho: the angle, in degrees counter-clockwise from +X, the arc starts at.</summary>
            public float ArcStart;

            public ParticleSystemShapeMultiModeValue ArcMode;
            public float ArcSpeed;
            public float RandomPosition;

            /// <summary>
            /// Emit from a strip laid along the crescent whose normals point
            /// along the sweep, instead of from the circle, whose particles all
            /// leave radially from the centre. <see cref="ArcMode"/> and
            /// <see cref="ArcSpeed"/> then drive the walk along the strip.
            /// </summary>
            public bool TangentEmit;

            /// <summary>(from, to) crescent t the strip covers, 0 tail → 1 head; may overshoot either end.</summary>
            public Vector2 EmitT;

            /// <summary>The strip's radius as a fraction of the crescent's outer radius at that t.</summary>
            public float EmitRadius;

            /// <summary>Half the strip's width, in local units.</summary>
            public float EmitJitter;

            /// <summary>Direction off the sweep tangent, degrees; positive tips it outward.</summary>
            public float EmitOutwardDegrees;

            /// <summary>Shape random direction amount.</summary>
            public float RandomDirection;

            /// <summary>Loop strips only: how far behind the head, in crescent t, particles are laid.</summary>
            public float EmitLag;

            public ParticleSystemRenderMode RenderMode;
            public float VelocityScale;
            public float LengthScale;

            /// <summary>Limit Velocity drag. Zero leaves the module off.</summary>
            public float Drag;

            public Keyframe[] SizeOverLife;
            public GradientColorKey[] Colors;
            public GradientAlphaKey[] Alphas;

            /// <summary>Zero leaves the noise module off.</summary>
            public float NoiseStrength;
            public float NoiseFrequency;
            public float NoiseScroll;

            /// <summary>Velocity over lifetime: orbit about local Z and push out radially.</summary>
            public bool Orbit;
            public float OrbitalZ;
            public float Radial;
        }

        /// <summary>
        /// One system of the hit impact. Much less than a <see cref="SpraySpec"/>:
        /// the impact is one burst at one point, placed by the VFX manager, so
        /// it has no strip to lay out and no reveal to follow.
        /// </summary>
        private sealed class ImpactSpec
        {
            public string Name;
            public MaterialSpec Material;

            /// <summary>Particles in the single burst at t = 0.</summary>
            public int Count;

            /// <summary>(min, max) seconds.</summary>
            public Vector2 Lifetime;

            /// <summary>(min, max) units per second. Zero leaves the particle where it was born, simulated locally.</summary>
            public Vector2 Speed;

            /// <summary>(min, max) units, at scale one.</summary>
            public Vector2 Size;

            /// <summary>Start colour; two different values pick randomly between them.</summary>
            public Color ColorA;
            public Color ColorB;

            public float Gravity;

            /// <summary>
            /// Half-angle, degrees, of the cone emitted along the system's +Z.
            /// Negative leaves the shape off: born on the origin, no direction.
            /// </summary>
            public float ConeAngle;

            /// <summary>The cone's base radius, in local units.</summary>
            public float ConeRadius;

            /// <summary>Turn this child so its +Z is the slash direction (the root's +Y).</summary>
            public bool AlongSwing;

            /// <summary>Child position in the root's space: +Y along the slash, +Z toward the camera.</summary>
            public Vector3 Offset;

            public ParticleSystemRenderMode RenderMode;
            public float VelocityScale;
            public float LengthScale;

            /// <summary>Limit Velocity drag. Zero leaves the module off.</summary>
            public float Drag;

            public Keyframe[] SizeOverLife;
            public GradientColorKey[] Colors;
            public GradientAlphaKey[] Alphas;
        }

        private static Prop F(string name, float value) =>
            new Prop(name, PropKind.Float, new Vector4(value, 0f, 0f, 0f));

        private static Prop V(string name, float x, float y) =>
            new Prop(name, PropKind.Vector, new Vector4(x, y, 0f, 0f));

        private static Prop C(string name, float r, float g, float b, float a = 1f) =>
            new Prop(name, PropKind.Color, new Vector4(r, g, b, a));

        private static GradientAlphaKey[] Alpha(params (float time, float alpha)[] keys)
        {
            var result = new GradientAlphaKey[keys.Length];
            for (int i = 0; i < keys.Length; i++)
                result[i] = new GradientAlphaKey(keys[i].alpha, keys[i].time);
            return result;
        }

        private static GradientColorKey[] White() => new[]
        {
            new GradientColorKey(Color.white, 0f),
            new GradientColorKey(Color.white, 1f)
        };

        /// <summary><paramref name="count"/> evenly spaced times from <paramref name="first"/> to <paramref name="last"/>.</summary>
        private static float[] Spread(float first, float last, int count)
        {
            var result = new float[count];
            for (int i = 0; i < count; i++)
                result[i] = count > 1 ? Mathf.Lerp(first, last, i / (float)(count - 1)) : first;
            return result;
        }

        private static Keyframe[] Line(float from, float to)
        {
            float slope = to - from;
            return new[]
            {
                new Keyframe(0f, from, slope, slope),
                new Keyframe(1f, to, slope, slope)
            };
        }

        /// <summary>Starts flat and falls away late: from - (from - to) * t^2.</summary>
        private static Keyframe[] EaseIn(float from, float to)
        {
            float slope = 2f * (to - from);
            return new[]
            {
                new Keyframe(0f, from, 0f, 0f),
                new Keyframe(1f, to, slope, slope)
            };
        }

        /// <summary>Moves fast at first and settles: from + (to - from) * (1 - (1 - t)^2).</summary>
        private static Keyframe[] EaseOut(float from, float to)
        {
            float slope = 2f * (to - from);
            return new[]
            {
                new Keyframe(0f, from, slope, slope),
                new Keyframe(1f, to, 0f, 0f)
            };
        }

        #endregion
    }
}
