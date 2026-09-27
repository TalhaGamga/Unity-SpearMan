Shader "Game/Slash Arc"
{
    // One material draws every part of a slash: the base body, the highlight
    // riding on it, the accent streaks and the sparks. Which part a triangle
    // belongs to is carried in its vertex data, so the whole effect is a single
    // draw call and the shape work stays on the mesh where it can be tuned.
    //
    // The stroke's growth is geometry, not a shader reveal: the mesh is rebuilt
    // each frame for however far the blade has turned. All this shader animates
    // is the fade and the sparks drifting off the edge.
    //
    // uv0 = (u along the arc | corner u, v across the width | corner v,
    //        core width, edge falloff)
    // uv1 = (drift x, drift y, position along the stroke, kind: 0 ribbon / 1 spark)
    // colour = per-part tint and opacity
    Properties
    {
        [HDR] _CoreColor ("Core Colour", Color) = (1, 1, 1, 1)
        [HDR] _EdgeColor ("Edge Colour", Color) = (0.45, 0.85, 1, 1)
        [HDR] _GlowColor ("Glow Colour", Color) = (0.2, 0.55, 1, 1)

        _Brightness ("Brightness", Range(0, 6)) = 1.6
        _CoreSharpness ("Core Sharpness", Range(0.5, 8)) = 2
        _GlowIntensity ("Glow Intensity", Range(0, 2)) = 0.5

        _Fade ("Fade", Range(0, 1)) = 1
        _DriftPhase ("Spark Drift Phase", Range(0, 2)) = 0
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "SlashArc"
            Tags { "LightMode" = "UniversalForward" }

            // Premultiplied alpha: the shader hands over colour already scaled
            // by coverage, so one blend mode covers a soft body and a glowing
            // core without a second pass and without alpha ever exceeding one.
            Blend One OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float4 uv0 : TEXCOORD0;
                float4 uv1 : TEXCOORD1;
                float4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float4 uv0 : TEXCOORD0;
                float2 shape : TEXCOORD1; // x: position along the stroke, y: kind
                float4 color : COLOR;
            };

            // Full precision here on purpose: this block has to match the
            // material's property list exactly for the SRP batcher to accept
            // the shader, and half-typed scalars are where that check trips.
            CBUFFER_START(UnityPerMaterial)
                float4 _CoreColor;
                float4 _EdgeColor;
                float4 _GlowColor;
                float _Brightness;
                float _CoreSharpness;
                float _GlowIntensity;
                float _Fade;
                float _DriftPhase;
            CBUFFER_END

            Varyings vert (Attributes input)
            {
                Varyings output = (Varyings)0;

                float3 positionOS = input.positionOS.xyz;

                // Sparks drift off the stroke by how far back along it they
                // sit, so the ones the blade passed longest ago have travelled
                // furthest. Every corner of a spark carries the same drift, so
                // the quad moves as one instead of stretching.
                float kind = input.uv1.w;
                positionOS.xy += input.uv1.xy * (input.uv1.z + _DriftPhase) * kind;

                output.positionCS = TransformObjectToHClip(positionOS);
                output.uv0 = input.uv0;
                output.shape = float2(input.uv1.z, kind);
                output.color = input.color;
                return output;
            }

            half4 frag (Varyings input) : SV_Target
            {
                // Distance from the middle of whatever this triangle is: across
                // the ribbon for a ribbon, radially for a spark.
                half ribbon = abs(input.uv0.y * 2.0 - 1.0);
                half blob = saturate(length(input.uv0.xy * 2.0 - 1.0));
                half kind = input.shape.y;
                half d = lerp(ribbon, blob, kind);

                half coreWidth = max(input.uv0.z, 1e-3);
                half falloff = max(input.uv0.w, 0.1);

                half body = pow(saturate(1.0 - d), falloff);
                half halo = pow(saturate(1.0 - d), 0.7) * _GlowIntensity;
                half core = pow(saturate(1.0 - d / coreWidth), _CoreSharpness);

                // White in the middle, the attack's colour at the edges, the
                // glow colour in the soft skirt beyond the body.
                half3 rgb = lerp(_EdgeColor.rgb, _CoreColor.rgb, core);
                rgb = lerp(_GlowColor.rgb, rgb, saturate(body));

                // The far end of the stroke sits back in time, so it reads
                // dimmer than the part still on the blade.
                half along = input.shape.x;
                half tail = lerp(1.0 - 0.35 * along * along, 1.0, kind);

                half alpha = saturate(body + halo * 0.5) * input.color.a * tail * _Fade;
                rgb *= input.color.rgb * _Brightness;

                return half4(rgb * alpha, alpha);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
