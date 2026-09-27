Shader "Game/Slash Spark"
{
    // Every loose particle a slash sheds - sparks, wisps, motes and embers -
    // is this one dot. It is drawn rather than sampled because the shape it
    // needs is nothing more than a falloff from the centre, and because the
    // particle renderer does the rest: a stretched billboard pulls the round
    // dot out along its velocity into a streak, so the same shader gives a
    // hot pinpoint at rest and a flick while it is moving, with no texture to
    // blur or pixelate at either size.
    //
    // The hot core is a second, tighter falloff tinted towards _CoreColor, so
    // a spark reads as white-hot in the middle and coloured at its edge -
    // which is what sells it as light rather than a coloured blob. Zero core
    // size turns the core off entirely, for particles that should stay flat.
    //
    // Premultiplied alpha, for the same reason as the slash body: the colour
    // leaves here already scaled by coverage, and _Additive decides how much
    // of the background the particle also covers. At one it covers nothing
    // and only adds light (sparks, motes); at zero it is an ordinary
    // alpha-blended speck, which is the only way the dark embers can darken
    // what is behind them. Additive embers would just vanish into the glow
    // they are meant to contrast with. One blend mode, one material setup,
    // both kinds of particle.
    //
    // A ring radius turns the dot into a hollow ring of the same falloff, for
    // the shock ring of a hit: light only where the edge of the impact is, so
    // the target stays visible through the middle of it. The core is still
    // added the same way, so a ring material sets its core size to zero.
    //
    // The silhouette fade is Slash Layered's: it thins a particle that sits
    // right in front of an opaque surface, so a spray over a fighter does not
    // paint out the outline the player is reading. Off at zero.
    //
    // uv0 = the billboard quad's (0..1, 0..1); the dot is centred on it.
    // colour = the particle's colour over lifetime: rgb tints, a fades.
    Properties
    {
        // [HDR] and linear, for the reason Slash Layered gives: an [HDR]
        // Color reaches the shader exactly as stored, so the picker, the
        // builder and the shader agree - and the picker can go past one.
        [HDR] _Color ("Colour", Color) = (3.5, 1.6, 3.2, 1)
        [HDR] _CoreColor ("Core Colour", Color) = (8, 7, 9, 1)

        // Radius of the hot core as a fraction of the dot's. Zero is no core.
        _CoreSize ("Core Size", Range(0, 1)) = 0.35

        // How quickly the dot fades towards its rim. High values make a tight
        // pinpoint inside a mostly empty quad; low values a soft blob.
        _Falloff ("Falloff", Range(0.2, 8)) = 2

        _Intensity ("Intensity", Range(0, 4)) = 1

        // One adds light only; zero blends over the background like paint.
        _Additive ("Additive", Range(0, 1)) = 1

        // Where the ring sits, as a fraction of the dot's radius, and how far
        // its falloff reaches either side of that. Zero radius is the dot.
        _Ring ("Ring Radius (0 = dot)", Range(0, 1)) = 0
        _RingWidth ("Ring Width", Range(0.01, 1)) = 0.2

        // As on Slash Layered: how much of the particle to take away on
        // contact with a surface behind it, the eye distance behind it that
        // still counts as contact, and the share of that distance faded in
        // full before the fade ramps off.
        _SilhouetteFade ("Silhouette Fade", Range(0, 1)) = 0
        _SilhouetteDepth ("Silhouette Depth", Float) = 0.8
        _SilhouetteHold ("Silhouette Hold", Range(0, 0.95)) = 0
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
            Name "SlashSpark"
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
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            // In the particle system's stream order - Position, Color, UV - so
            // the colour is read from where the particle system put it; a
            // particle's vertex data follows the order its streams are listed.
            struct Attributes
            {
                float4 positionOS : POSITION;
                float4 color : COLOR;

                // Four channels even though only the first two are read: a
                // system with extra vertex streams enabled packs them into this
                // slot behind the UV, and taking the whole slot keeps the dot
                // correct whatever else a prefab author switches on.
                float4 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;

                // This point's eye depth, for the silhouette fade.
                float eyeDepth : TEXCOORD1;
            };

            // Full-precision scalars throughout, mirroring the Properties
            // block exactly, so the SRP Batcher accepts the material.
            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float4 _CoreColor;
                float _CoreSize;
                float _Falloff;
                float _Intensity;
                float _Additive;
                float _Ring;
                float _RingWidth;
                float _SilhouetteFade;
                float _SilhouetteDepth;
                float _SilhouetteHold;
            CBUFFER_END

            // Slash Layered's silhouette fade, kept word for word so a spray and
            // the sheet it comes off thin over a body the same way. Less of the
            // particle the closer an opaque surface sits behind it; a scene
            // depth clearly in front of the particle can only be a missing or
            // stale depth texture (it would have failed the depth test), and
            // then the particle is left whole.
            float silhouetteKeep(float4 positionCS, float fragEye)
            {
                if (_SilhouetteFade <= 0.0)
                    return 1.0;

                float rawDepth = SampleSceneDepth(GetNormalizedScreenSpaceUV(positionCS));
                float sceneEye = IsPerspectiveProjection()
                    ? LinearEyeDepth(rawDepth, _ZBufferParams)
                    : LinearDepthToEyeDepth(rawDepth);

                float gap = sceneEye - fragEye;
                float depthRange = max(_SilhouetteDepth, 1e-3);
                float hold = _SilhouetteHold * depthRange;
                float near = 1.0 - saturate((gap - hold) / max(depthRange - hold, 1e-3));
                near *= step(-0.05, gap);
                return 1.0 - _SilhouetteFade * near;
            }

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv.xy;
                output.color = input.color;

                // From the view matrix, not the depth buffer value: right for
                // either projection, and linear across the quad.
                output.eyeDepth = LinearEyeDepth(TransformObjectToWorld(input.positionOS.xyz), GetWorldToViewMatrix());
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                // Zero at the centre, one at the quad's inscribed circle, and
                // beyond one in the corners, which the saturate below empties.
                float d = length(input.uv * 2.0 - 1.0);

                float dotShape = pow(saturate(1.0 - d), _Falloff);

                // The same falloff, measured from a circle instead of the
                // centre. Cut at the inscribed circle, which the dot never
                // reaches past on its own: a wide ring would otherwise run on
                // into the quad's corners and show its square. The cut ramps
                // over a pixel so the ring's outside does not stair-step.
                float ringShape = pow(saturate(1.0 - abs(d - _Ring) / max(_RingWidth, 1e-3)), _Falloff)
                    * saturate((1.0 - d) / max(fwidth(d), 1e-4));
                float shape = _Ring > 0.0 ? ringShape : dotShape;

                // The divide is kept safe rather than branched around: the
                // size is clamped off zero so the division is always finite,
                // and the step then switches the core off outright, since a
                // clamped size would otherwise still light the single texel
                // sitting exactly on the centre.
                float coreSize = max(_CoreSize, 1e-4);
                float core = pow(saturate(1.0 - d / coreSize), 2.0)
                    * step(1e-4, _CoreSize);

                float3 rgb = lerp(_Color.rgb, _CoreColor.rgb, core)
                    * input.color.rgb * _Intensity;

                float alpha = shape * input.color.a * _Color.a;

                // Before the premultiply, so an additive spark thins too.
                alpha *= silhouetteKeep(input.positionCS, input.eyeDepth);

                return half4(rgb * alpha, alpha * (1.0 - _Additive));
            }
            ENDHLSL
        }
    }

    Fallback Off
}
