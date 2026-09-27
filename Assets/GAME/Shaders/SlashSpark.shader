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
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv.xy;
                output.color = input.color;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                // Zero at the centre, one at the quad's inscribed circle, and
                // beyond one in the corners, which the saturate below empties.
                float d = length(input.uv * 2.0 - 1.0);

                float shape = pow(saturate(1.0 - d), _Falloff);

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

                return half4(rgb * alpha, alpha * (1.0 - _Additive));
            }
            ENDHLSL
        }
    }

    Fallback Off
}
