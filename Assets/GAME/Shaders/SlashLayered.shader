Shader "Game/Slash Layered"
{
    // One shader for every sheet of the crescent slash - the soft glow, the
    // teal brush on the inside, the violet body - each a material of its own.
    //
    // The layers do not get geometry of their own. They all draw the same
    // band, and each one carves its sheet out of it with a window across the
    // width (_Across) that narrows and drifts towards the tail. Sharing the
    // band is what makes them slide against each other the way the reference
    // does: they follow one path exactly, yet the brush can sit on the inner
    // radius and run longer while the body tapers onto the outer rim and the
    // glow spills past both. Giving each its own mesh would mean keeping three
    // meshes in agreement with every swing; a window is one number to tune.
    //
    // Three things drive it, and all three hand over the same data:
    //   - a particle system drawing the crescent mesh, the standalone prefab
    //   - SlashEffect's band, built from the blade's path (SlashShapeBuilder)
    //   - SlashEffect's ring (SlashCircleBuilder)
    //
    // uv0    = (u along the stroke, v across it 0 inner -> 1 outer, hide, seed)
    //          zw are the particle's Custom1 stream. The meshes write zeros, so
    //          zero has to mean "left alone": fully revealed, default pattern.
    // colour = rgb tint, and a is life - not opacity (see below)
    //
    // u runs from the tail. On the band it is distance travelled and is not
    // normalised, so the pattern stays pinned to where the blade has been
    // while the stroke grows; _HeadU is where the head currently is, and
    // u / _HeadU is what places the colour ramp and the taper, so the head
    // is always at one however long the swing got. On the particle mesh u is
    // already 0..1 and the hide channel pulls the head back along it instead,
    // which is how a mesh that exists in full still sweeps in.
    //
    // Premultiplied, like Slash Arc, so one blend mode covers both ends of the
    // value range: the dark indigo streaks really darken what is behind them
    // while the rim and head add light past one for bloom. _Additive slides a
    // layer from covering to purely adding (the glow) without a second shader.
    //
    // Life is a dissolve threshold, not an opacity. As it falls, noise, the
    // tail and the inner edge are eaten first and a burning edge runs ahead of
    // the erosion. Fading alpha instead turns a hard, readable cut into a
    // smear, which is the one thing a stylised slash must never do.
    Properties
    {
        [Header(Window)]
        _Across ("Across (x inner, y outer)", Vector) = (0.3, 0.88, 0, 0)
        _Anchor ("Taper Anchor (0 inner, 1 outer)", Range(0, 1)) = 1
        _TailCut ("Tail Cut", Range(0, 0.95)) = 0.3
        _TailWidth ("Tail Width", Range(0, 1)) = 0.06
        _WidthPeak ("Width Peak", Range(0.05, 1)) = 0.8
        _HeadWidth ("Head Width", Range(0, 1)) = 0.85
        _TaperPower ("Taper Power", Range(0.2, 4)) = 1.4
        _TailDrift ("Tail Drift", Range(-0.6, 0.6)) = 0
        _HeadSoft ("Head Soft", Range(0, 0.3)) = 0.02
        _HeadCap ("Head Cap (u)", Range(0, 0.2)) = 0
        _HeadCapWidth ("Head Cap Width", Range(0.05, 1)) = 0.5
        _HeadFray ("Head Fray (u)", Range(0, 0.1)) = 0

        [Header(Edges)]
        _EdgeSoft ("Edge Softness", Range(0.001, 0.5)) = 0.03
        _InnerRough ("Inner Roughness", Range(0, 1)) = 0.5
        _OuterRough ("Outer Roughness", Range(0, 0.5)) = 0.03
        _TailFray ("Tail Fray", Range(0, 1)) = 0.5
        _EdgeNoiseScale ("Edge Noise Scale", Vector) = (7, 3, 0, 0)
        _EdgeSkew ("Edge Skew (+ tongues lean to the tail)", Range(-1, 1)) = 0

        [Header(Noise)]
        _NoiseScale ("Noise Scale (x along, y across)", Vector) = (2.2, 10, 0, 0)
        _NoiseScroll ("Noise Scroll", Float) = 0.5
        _NoiseWarp ("Noise Warp", Range(0, 2)) = 0.8
        _Seed ("Seed", Float) = 0
        _WarpScale ("Warp Scale (x along, y across, x noise scale)", Vector) = (0.5, 0.7, 0, 0)
        _WarpScroll ("Warp Scroll (x Noise Scroll)", Range(0, 1)) = 1

        // [HDR], and linear. An [HDR] Color is uploaded exactly as stored -
        // Unity linearises only plain Colors - so the value the inspector's
        // HDR picker shows, the value the builder's Look block states and the
        // value this shader receives are one and the same number. A plain
        // Color could get there too through a gamma round trip, but its picker
        // clamps at one, and a rim or a head that cannot be pushed past one
        // in the inspector cannot be tuned for bloom.
        [Header(Colour)]
        [HDR] _HeadColor ("Head Colour", Color) = (3.2, 1.9, 3.6, 1)
        [HDR] _MidColor ("Mid Colour", Color) = (1.4, 0.3, 2.4, 1)
        [HDR] _TailColor ("Tail Colour", Color) = (0.25, 0.2, 1.6, 1)
        _MidPoint ("Mid Point", Range(0.05, 0.95)) = 0.55
        [HDR] _ShadowColor ("Shadow Colour", Color) = (0.07, 0.03, 0.4, 1)
        _InnerShade ("Inner Shade", Range(0, 1)) = 0.5
        _InnerShadePower ("Inner Shade Power", Range(0.2, 6)) = 1.6
        _StreakDark ("Dark Streaks", Range(0, 1)) = 0.55
        _StreakDarkThreshold ("Dark Streak Threshold", Range(0, 1)) = 0.38
        _StreakBright ("Bright Streaks", Range(0, 2)) = 0.6
        _StreakBrightThreshold ("Bright Streak Threshold", Range(0, 1)) = 0.65
        [HDR] _HighlightColor ("Highlight Colour", Color) = (2.4, 1.5, 3.0, 1)
        _StreakSoft ("Streak Softness", Range(0.005, 0.5)) = 0.07
        _Posterize ("Posterize Steps (0 off)", Range(0, 8)) = 0
        _StreakBias ("Streak Bias (+ dark inside, bright outside)", Range(-1, 1)) = 0
        [HDR] _StreakDarkColor ("Dark Streak Colour", Color) = (0.07, 0.03, 0.4, 1)
        _HighlightRibbon ("Highlight Ribbon Width (0 = fill)", Range(0, 0.5)) = 0
        _Bristle ("Bristle Streaks", Range(0, 1)) = 0
        _BristleThreshold ("Bristle Threshold", Range(0, 1)) = 0.66
        _MarbleTone ("Marble Tone", Range(0, 1)) = 0

        [Header(Rim)]
        [HDR] _RimColor ("Rim Colour", Color) = (6, 5, 7, 1)
        _RimWidth ("Rim Width", Range(0, 0.5)) = 0.07
        _RimMinWidth ("Rim Min Width (v)", Range(0, 0.1)) = 0
        _RimSoft ("Rim Softness", Range(0.001, 0.3)) = 0.04
        _RimTail ("Rim At Tail", Range(0, 1)) = 0.12
        _RimPower ("Rim Power", Range(0.2, 6)) = 1.2
        _HeadHot ("Head Hot Spot", Range(0, 4)) = 1.2
        _HeadHotPower ("Head Hot Power", Range(0.5, 16)) = 5
        _RimDark ("Rim Dark Band", Range(0, 1)) = 0
        _RimDarkWidth ("Rim Dark Width", Range(0, 0.4)) = 0.12

        [Header(Opacity)]
        _Intensity ("Intensity", Range(0, 4)) = 1
        _Opacity ("Opacity", Range(0, 1)) = 1
        _AlphaNoise ("Alpha Noise", Range(0, 1)) = 0.15
        _Additive ("Additive", Range(0, 1)) = 0.15

        [Header(Dissolve)]
        _DissolveNoise ("Dissolve Noise Weight", Range(0, 1)) = 0.6
        _DissolveTail ("Dissolve Tail Weight", Range(0, 1)) = 0.5
        _DissolveInner ("Dissolve Inner Weight", Range(0, 1)) = 0.3
        _DissolveSoft ("Dissolve Softness", Range(0.001, 0.3)) = 0.05
        [HDR] _BurnColor ("Burn Colour", Color) = (3, 1.5, 4, 1)
        _BurnWidth ("Burn Width", Range(0, 0.3)) = 0.06
        _DissolveNoiseScale ("Dissolve Noise Scale (x along, y across)", Vector) = (3, 16, 0, 0)
        _DissolveBias ("Dissolve Bias (+ goes earlier)", Range(-0.5, 0.5)) = 0

        // Pushed per frame by SlashEffect on the band and ring; left at their
        // defaults under a particle system, where colour alpha and the hide
        // channel do the same jobs.
        [Header(Runtime)]
        _Life ("Life", Range(0, 1)) = 1
        _HeadU ("Head U", Float) = 1
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
            "PreviewType" = "Plane"
        }

        Pass
        {
            Name "SlashLayered"
            Tags { "LightMode" = "UniversalForward" }

            Blend One OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Off

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            // In the particle system's stream order - Position, Color, UV,
            // Custom1XY - and not merely with the right semantics. A mesh
            // particle's vertices are handed over laid out in the order the
            // streams are listed, and with the colour declared after the UVs
            // it read the custom data instead: life came through at a quarter,
            // and the whole crescent dissolved before it was ever seen.
            struct Attributes
            {
                float4 positionOS : POSITION;
                float4 color : COLOR;

                // Four channels: the particle system packs its UV stream into
                // xy and Custom1.xy into zw. The band and ring leave zw at zero.
                float4 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float4 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            // Full precision and nothing but the properties: the SRP batcher
            // only accepts the shader when this block matches the material's
            // property list, and half-typed scalars are where that check trips.
            // Kept in the Properties' order so the two are easy to hold side
            // by side.
            CBUFFER_START(UnityPerMaterial)
                float4 _Across;
                float _Anchor;
                float _TailCut;
                float _TailWidth;
                float _WidthPeak;
                float _HeadWidth;
                float _TaperPower;
                float _TailDrift;
                float _HeadSoft;
                float _HeadCap;
                float _HeadCapWidth;
                float _HeadFray;

                float _EdgeSoft;
                float _InnerRough;
                float _OuterRough;
                float _TailFray;
                float4 _EdgeNoiseScale;
                float _EdgeSkew;

                float4 _NoiseScale;
                float _NoiseScroll;
                float _NoiseWarp;
                float _Seed;
                float4 _WarpScale;
                float _WarpScroll;

                float4 _HeadColor;
                float4 _MidColor;
                float4 _TailColor;
                float _MidPoint;
                float4 _ShadowColor;
                float _InnerShade;
                float _InnerShadePower;
                float _StreakDark;
                float _StreakDarkThreshold;
                float _StreakBright;
                float _StreakBrightThreshold;
                float4 _HighlightColor;
                float _StreakSoft;
                float _Posterize;
                float _StreakBias;
                float4 _StreakDarkColor;
                float _HighlightRibbon;
                float _Bristle;
                float _BristleThreshold;
                float _MarbleTone;

                float4 _RimColor;
                float _RimWidth;
                float _RimMinWidth;
                float _RimSoft;
                float _RimTail;
                float _RimPower;
                float _HeadHot;
                float _HeadHotPower;
                float _RimDark;
                float _RimDarkWidth;

                float _Intensity;
                float _Opacity;
                float _AlphaNoise;
                float _Additive;

                float _DissolveNoise;
                float _DissolveTail;
                float _DissolveInner;
                float _DissolveSoft;
                float4 _BurnColor;
                float _BurnWidth;
                float4 _DissolveNoiseScale;
                float _DissolveBias;

                float _Life;
                float _HeadU;
            CBUFFER_END

            // ---------------------------------------------------------------
            // Noise. Procedural so the layers need no textures and the pattern
            // can be stretched along the stroke without a texture's pixels
            // stretching with it.

            // PCG-style 2D integer hash (Jarzynski & Olano). Integer, not the
            // usual frac(sin(...)): sin-based hashes band and repeat once
            // their input grows, and ours grows forever - the pattern scrolls
            // with time and every particle adds its own seed offset. Integer
            // cells hash exactly at any distance from the origin.
            uint2 pcg2d(uint2 v)
            {
                v = v * 1664525u + 1013904223u;
                v.x += v.y * 1664525u;
                v.y += v.x * 1664525u;
                v ^= v >> 16u;
                v.x += v.y * 1664525u;
                v.y += v.x * 1664525u;
                v ^= v >> 16u;
                return v;
            }

            // Gradient at a lattice corner, each component in [-1, 1). Through
            // int first: a negative float straight to uint is undefined.
            float2 latticeGradient(float2 cell)
            {
                uint2 h = pcg2d(asuint(int2(cell)));
                return float2(h >> 8u) * (2.0 / 16777216.0) - 1.0;
            }

            // Signed 2D gradient noise, within about +-0.75. Quintic fade
            // rather than the cubic one because the edges are carved at the
            // noise's slopes: cubic has a crease in its derivative at every
            // cell border, and a hard edge drawn along a crease shows the grid.
            float gradientNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = p - i;
                float2 fade = f * f * f * (f * (f * 6.0 - 15.0) + 10.0);

                float n00 = dot(latticeGradient(i), f);
                float n10 = dot(latticeGradient(i + float2(1.0, 0.0)), f - float2(1.0, 0.0));
                float n01 = dot(latticeGradient(i + float2(0.0, 1.0)), f - float2(0.0, 1.0));
                float n11 = dot(latticeGradient(i + float2(1.0, 1.0)), f - float2(1.0, 1.0));

                return lerp(lerp(n00, n10, fade.x), lerp(n01, n11, fade.x), fade.y);
            }

            // Between octaves the domain is rotated (~37 degrees) as well as
            // doubled, and nudged off the origin, so the octaves' lattices
            // never line up and no axis-aligned grain builds up. The rotation
            // happens in pattern space, after the stretch, so the streaks
            // still run along the stroke.
            float2 nextOctave(float2 p)
            {
                return float2(1.6 * p.x + 1.2 * p.y, -1.2 * p.x + 1.6 * p.y) + float2(17.0, 5.3);
            }

            // Three octaves, lacunarity 2, gain 0.5, brought to ~[0, 1]. The
            // plain average of the octaves only has a spread of about +-0.27
            // (1st to 99th percentile, measured), so remapping by the
            // theoretical bound would crowd everything around 0.5 and leave
            // the streak thresholds with next to nothing either side of them.
            // The gain stretches that measured spread onto roughly 0.04..0.96.
            float fbm3(float2 p)
            {
                float n = gradientNoise(p);
                p = nextOctave(p);
                n += 0.5 * gradientNoise(p);
                p = nextOctave(p);
                n += 0.25 * gradientNoise(p);
                return saturate(0.5 + n * (1.71 / 1.75));
            }

            // Two octaves for the edges: they only need to break the line up,
            // not carry the marbling. Measured spread about +-0.31, stretched
            // the same way onto roughly 0.04..0.96.
            float fbm2(float2 p)
            {
                float n = gradientNoise(p);
                p = nextOctave(p);
                n += 0.5 * gradientNoise(p);
                return saturate(0.5 + n); // average n / 1.5, times a gain of 1.5
            }

            // smoothstep that tolerates a zero-width ramp. Several ramps here
            // are widths the artist can drag to zero, and the built-in one
            // divides by that width.
            float smoothstepSafe(float edge0, float edge1, float x)
            {
                float t = saturate((x - edge0) / max(edge1 - edge0, 1e-4));
                return t * t * (3.0 - 2.0 * t);
            }

            Varyings vert (Attributes input)
            {
                Varyings output = (Varyings)0;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                output.color = input.color;
                return output;
            }

            half4 frag (Varyings input) : SV_Target
            {
                float u = input.uv.x;
                float v = input.uv.y;
                float hide = saturate(input.uv.z);
                float seed = input.uv.w;

                // ---- Where along the stroke ----------------------------------

                float life = saturate(input.color.a * _Life);

                // Hiding pulls the head back towards the tail, so the particle
                // mesh sweeps in over its reveal. The band never hides: there
                // the mesh ends where the blade is and _HeadU says where that is.
                float headU = max(_HeadU * (1.0 - hide), 1e-4);
                float a = saturate(u / headU);

                // Only matters while revealing: the band has nothing past its
                // head to cut away.
                float front = 1.0 - smoothstepSafe(headU, headU + 0.02, u);

                // This layer's own 0..1 along, from its tail to the head. Layers
                // end at different distances back, which is how the brush runs
                // longer than the body over the very same band.
                float along = saturate((a - _TailCut) / max(1.0 - _TailCut, 1e-3));

                // ---- The window across the band ------------------------------

                // Width as a fraction of the head's window: swells from the tail
                // to its peak, then eases to its width at the head itself.
                float rise = lerp(_TailWidth, 1.0,
                    pow(saturate(along / max(_WidthPeak, 1e-4)), _TaperPower));
                float fall = lerp(1.0, _HeadWidth,
                    saturate((along - _WidthPeak) / max(1.0 - _WidthPeak, 1e-3)));
                float widthProfile = along <= _WidthPeak ? rise : fall;

                float full = _Across.y - _Across.x;
                float width = full * widthProfile;

                // The taper closes onto the anchor edge, not the middle: the
                // body keeps its clean outer rim all the way to a point while
                // its inner side peels away. The drift slides the whole window
                // across as it goes back, which turns an arc into a spiral.
                float centreFixed = lerp(_Across.x + width * 0.5, _Across.y - width * 0.5, _Anchor);
                float shift = _TailDrift * (1.0 - along);
                float inner = centreFixed - width * 0.5 + shift;
                float outer = centreFixed + width * 0.5 + shift;

                // 0 at this layer's inner edge, 1 at its outer one. Everything
                // across - edges, shading, rim - reads this, not v.
                float windowWidth = max(outer - inner, 1e-4);
                float x = (v - inner) / windowWidth;

                // ---- Noise ---------------------------------------------------

                // Sampled on the mesh's u, not on a, so the pattern stays put
                // on the band while the head moves on. The particle seed and the
                // material seed together keep two layers, or two slashes, from
                // wearing the same marbling.
                float2 seedOffset = (seed + _Seed) * float2(17.13, 31.71);

                // Stretched along the stroke, so the marbling reads as flow
                // streaks rather than blobs, and scrolled from head to tail as
                // if the stroke were still pouring out of the blade.
                float2 p0 = float2(u * _NoiseScale.x, v * _NoiseScale.y) + seedOffset;
                float scroll = _Time.y * _NoiseScroll;
                float2 p = p0 + float2(scroll, 0.0);

                // One level of domain warp is what makes streaks look liquid
                // instead of like stretched clouds. The displacement is mostly
                // ACROSS the stroke: sliding a stretched streak along its own
                // length is invisible, while pushing it sideways is what bends
                // it into S-folds and pinches. The warp has its own frequency
                // (in pattern space, so relative to the marble's) and its own,
                // usually slower, scroll: the streaks then pour through bends
                // that drift more slowly than they do, so the surface morphs
                // instead of sliding by like a conveyor. The defaults (0.5, 0.7)
                // and 1 are the rigid warp this shader always had.
                float2 pw = p0 + float2(scroll * _WarpScroll, 0.0);
                float warp = fbm3(pw * _WarpScale.xy + 5.2);
                float marble = fbm3(p + _NoiseWarp * (warp - 0.5) * 2.0 * float2(0.3, 1.0));

                // Edges do not scroll: an edge that swims reads as jelly, not as
                // a cut. The outer edge gets its own sample so the two sides
                // never wobble in step; one octave is enough for an edge that
                // is meant to stay nearly clean. The skew shears the pattern so
                // the tongues it carves lean back towards the tail, as if the
                // paint had been dragged, instead of standing up as lumps.
                float2 pe = float2(u * _EdgeNoiseScale.x - v * _EdgeNoiseScale.y * _EdgeSkew,
                    v * _EdgeNoiseScale.y) + seedOffset + 11.7;
                float edgeNoise = fbm2(pe);
                float edgeNoiseOuter = saturate(0.5 + gradientNoise(pe + 23.1) * 1.3);

                // The dissolve's own noise, stretched along the stroke and never
                // scrolled, so the pieces stay where they are while they burn
                // away instead of crawling with the marbling. Sampled here
                // because the brush also reads it for its bristle marks.
                float2 pd = float2(u * _DissolveNoiseScale.x, v * _DissolveNoiseScale.y) + seedOffset + 41.3;
                float dissolveNoise = fbm2(pd) * 0.75 + edgeNoise * 0.25;

                // ---- Coverage ------------------------------------------------

                // Every hard cut ramps over at least a pixel and a half. The
                // softness is relative to the window, so it shrinks as the window
                // narrows: at the body's needle tail and in the brush's flicks it
                // would fall far below a pixel and the thinnest, most important
                // parts of the contour would shimmer into dots at game distance.
                float s = _EdgeSoft;

                // Ragged inside, clean(ish) outside: the leading edge of the
                // cut is the one the eye follows, so that one stays crisp.
                float argInner = x - edgeNoise * _InnerRough;
                float argOuter = (1.0 - x) - edgeNoiseOuter * _OuterRough;
                float maskInner = smoothstepSafe(0.0, max(s, fwidth(argInner) * 1.5), argInner);
                float maskOuter = smoothstepSafe(0.0, max(s, fwidth(argOuter) * 1.5), argOuter);

                // The fray grows towards the tail, so the thin end breaks into
                // separate flicks instead of just getting narrower.
                float argTail = along - edgeNoise * _TailFray * (1.0 - along);
                float maskTail = smoothstepSafe(0.0, max(s * 2.0, fwidth(argTail) * 1.5), argTail)
                    * step(0.0001, a - _TailCut + 1e-4);

                float maskHead = _HeadSoft > 1e-3 ? smoothstepSafe(0.0, _HeadSoft, 1.0 - a) : 1.0;

                // The head's outer corner, rounded back in u rather than cut flat
                // along the blade: a quarter-round shoulder that starts at the
                // tip and curves into the rim, so the head reads as a comma and
                // not a D. Measured in u, not a, so it keeps one world size however
                // long the swing is. The fray pulls the head back in ragged
                // bristle ends instead, so a layer can trail just behind the blade.
                float capX = saturate((x - (1.0 - _HeadCapWidth)) / max(_HeadCapWidth, 1e-3));
                float capBack = _HeadCap * (1.0 - sqrt(saturate(1.0 - capX * capX)))
                    + _HeadFray * edgeNoise;
                float argCap = (headU - u) - capBack;
                float capMask = smoothstepSafe(0.0, max(0.004, fwidth(argCap) * 1.5), argCap);
                maskHead *= (_HeadCap + _HeadFray) > 1e-4 ? capMask : 1.0;

                float coverage = maskInner * maskOuter * maskTail * maskHead * front
                    * _Opacity * lerp(1.0, marble, _AlphaNoise);

                // ---- Colour --------------------------------------------------

                // Warm where the blade is now, cool where it was.
                float midPoint = _MidPoint;
                float3 towardsMid = lerp(_TailColor.rgb, _MidColor.rgb,
                    smoothstep(0.0, 1.0, a / max(midPoint, 1e-4)));
                float3 towardsHead = lerp(_MidColor.rgb, _HeadColor.rgb,
                    smoothstep(0.0, 1.0, (a - midPoint) / max(1.0 - midPoint, 1e-4)));
                float3 c = a < midPoint ? towardsMid : towardsHead;

                // Soft value inside each fold, so between the streaks the sheet
                // reads as a glossy volume rather than a cel fill.
                c *= 1.0 + _MarbleTone * (marble - 0.5) * 2.0;

                // Across, measured from the CARVED inner edge rather than the
                // window's, so shading hugs the ragged silhouette the way drawn
                // FX shading does instead of running as concentric stripes
                // inside it.
                float carvedEdge = edgeNoise * _InnerRough;
                float xs = saturate((x - carvedEdge) / max(1.0 - carvedEdge, 1e-3));

                // The bias tilts the marble across the stroke, so dark streaks
                // gather on one side and bright ones on the other - on the body,
                // dark indigo tongues growing out of the inner edge and pink
                // highlights gathering towards the rim.
                float mb = marble + (xs - 0.5) * _StreakBias;
                float streakSoft = max(_StreakSoft, fwidth(mb) * 0.75);

                // The shapes of the shading, before their strengths. Posterized
                // here rather than after the strength is applied: that way the
                // steps divide the shape itself, and a weak shade still gets its
                // bands instead of rounding away to nothing.
                float3 shape;
                shape.x = pow(saturate(1.0 - xs), _InnerShadePower);
                shape.y = 1.0 - smoothstepSafe(_StreakDarkThreshold - streakSoft,
                    _StreakDarkThreshold + streakSoft, mb);
                // Highlights either fill everything above the threshold, or -
                // with a ribbon width - only a band around it: thin lines that
                // follow the warped marble's iso-contours, pinching where it is
                // steep and opening into sheets where it flattens, which is
                // the glossy folded-liquid read a plateau cannot give.
                float fill = smoothstepSafe(_StreakBrightThreshold - streakSoft,
                    _StreakBrightThreshold + streakSoft, mb);
                float halfRibbon = _HighlightRibbon * 0.5;
                float ribbon = 1.0 - smoothstepSafe(halfRibbon - streakSoft, halfRibbon + streakSoft,
                    abs(mb - _StreakBrightThreshold));
                shape.z = _HighlightRibbon > 1e-4 ? ribbon : fill;

                float steps = max(floor(_Posterize), 1.0);
                float3 banded = floor(shape * steps + 0.5) / steps;
                shape = _Posterize >= 1.0 ? banded : shape;

                float shade = _InnerShade * shape.x;
                float dark = _StreakDark * shape.y;
                float bright = _StreakBright * shape.z;

                // Darkening lerps towards a colour rather than scaling down, so
                // the inner side and the streaks turn indigo (or, on the brush,
                // royal blue) instead of going muddy grey. The streaks have their
                // own colour so a layer can shade one way and streak another.
                c = lerp(c, _ShadowColor.rgb, saturate(shade));
                c = lerp(c, _StreakDarkColor.rgb, saturate(dark));

                // Dry-brush bristle marks, from the dissolve noise: no extra
                // octaves, and the marks sit exactly where the paint opens into
                // gaps as it erodes, so surface and breakup are one gesture.
                // Kept off the dark patches so those stay clean.
                float bristleSoft = max(_StreakSoft, fwidth(dissolveNoise) * 0.75);
                float bristle = smoothstepSafe(_BristleThreshold - bristleSoft,
                    _BristleThreshold + bristleSoft, dissolveNoise);
                c = lerp(c, _ShadowColor.rgb, saturate(bristle * _Bristle * (1.0 - dark)));

                c += _HighlightColor.rgb * bright * (1.0 - dark);

                // White-hot outer rim, strongest at the head. A layer with no
                // rim width gets no rim at all; left to the softness alone, a
                // sliver would still show along its outer edge. The minimum
                // width is in v, so the rim keeps a real thickness where the
                // window narrows to a needle - the body's tail ends as one white
                // line instead of a violet hair.
                float rimWeight = lerp(_RimTail, 1.0, pow(a, _RimPower));
                float rimX = min(max(_RimWidth, _RimMinWidth / windowWidth), 1.0);
                float rimIn = 1.0 - rimX;

                // A band of shadow framing the rim from inside: the contrast is
                // what makes the rim look white-hot without leaning on bloom,
                // which the game barely has. Broken up by the dark streaks so it
                // reads as marbling rather than a painted stripe.
                float subRim = smoothstepSafe(rimIn - _RimDarkWidth - _RimSoft, rimIn - _RimDarkWidth * 0.4, x)
                    * (1.0 - smoothstepSafe(rimIn - _RimSoft, rimIn, x));
                c = lerp(c, _ShadowColor.rgb,
                    saturate(subRim * _RimDark * rimWeight * lerp(1.0, shape.y, 0.5)));

                float rim = smoothstepSafe(rimIn - _RimSoft, rimIn + _RimSoft * 0.25,
                    x - edgeNoiseOuter * _OuterRough * 0.5);
                rim *= saturate(max(_RimWidth, _RimMinWidth) * 100.0);
                c = lerp(c, _RimColor.rgb, saturate(rim * rimWeight));

                // The head is where the energy is, and brightest towards the
                // outside, where the blade's tip is.
                c += _RimColor.rgb * _HeadHot * pow(a, _HeadHotPower) * saturate(x);

                c *= input.color.rgb * _Intensity;

                // ---- Dissolve ------------------------------------------------

                // How early each point goes: noisy, tail first, inside first -
                // the stroke is eaten from its old, thin side back to the rim.
                // The noise is the unscrolled dissolveNoise sampled above.
                float wn = _DissolveNoise;
                float wt = _DissolveTail;
                float wi = _DissolveInner;
                float d = saturate((dissolveNoise * wn + (1.0 - along) * wt + (1.0 - saturate(x)) * wi)
                    / max(wn + wt + wi, 1e-3));

                // Normalising by the weights leaves the visible body topping out
                // well short of one, which is a dead first quarter of life; the
                // bias moves the whole erosion earlier.
                d = saturate(d + _DissolveBias);

                // The threshold overshoots by the softness so that at full life
                // even the latest point is completely in, and at zero nothing is.
                // Floored at a pixel and a half, so a toon-hard cut anti-aliases
                // instead of crawling.
                float k = max(_DissolveSoft, fwidth(d) * 1.5);
                float lifeT = life * (1.0 + k);
                float visible = smoothstepSafe(d, d + k, lifeT);

                // A hot band running just ahead of the erosion. Held off at full
                // life so an untouched stroke has no burn line through it, and
                // off entirely when the layer asks for no burn width.
                float burn = (1.0 - smoothstepSafe(k, k + _BurnWidth, lifeT - d))
                    * visible * saturate((1.0 - life) * 8.0) * saturate(_BurnWidth * 100.0);
                c += _BurnColor.rgb * burn;

                float alpha = coverage * visible;

                // Premultiplied. Alpha is what this layer covers of what is
                // behind it; at _Additive 1 it covers nothing and only adds.
                return half4(c * alpha, alpha * (1.0 - _Additive));
            }
            ENDHLSL
        }
    }

    Fallback Off
}
