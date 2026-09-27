Shader "Game/Slash Particle"
{
    // The pack's own slash shader, brought over to URP.
    //
    // Nothing here draws the arc progressively, because nothing needs to: the
    // band is built from the blade's own path, so it only exists where the
    // weapon has already been and sweeps by construction. An earlier version
    // uncovered a ready-made ring with a shader parameter instead, which put
    // the burden of matching the swing on a value that knew nothing about it.
    //
    // The dissolve is the trailing edge. Its threshold is the surface's own
    // fading alpha, so as _Life falls the noise texture eats the shape away
    // from the thin end inward. That erosion is what turns a flat scratch
    // texture into brush strokes; soften it into a plain alpha fade and the cut
    // reads as a smear instead.
    //
    // _Life stands in for the particle alpha the pack fed this, so the same
    // material works whether it is drawn by a particle system or by a mesh
    // renderer whose values are pushed per frame.
    Properties
    {
        [HDR] _TintColor ("Tint", Color) = (1, 1, 1, 1)
        _MainTex ("Texture", 2D) = "white" {}

        [Toggle(USE_ALPHA_CUTOUT)] _UseAlphaCutout ("Dissolve With Alpha", Float) = 0
        _CutoutTex ("Dissolve Texture", 2D) = "white" {}

        _DistTex ("Distortion Texture", 2D) = "white" {}
        _DistStrength ("Distortion Strength (xy) and Scroll (zw)", Vector) = (0, 0, 0, 0)

        // How much of the surface is left. One is whole, zero is gone.
        // Driven per frame on the band; left at one everywhere else.
        _Life ("Life", Range(0, 1)) = 1
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
            Name "SlashParticle"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma shader_feature_local_fragment USE_ALPHA_CUTOUT
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;

                // Four channels, not two: the particle system writes an extra
                // pair here that the pack's textures are scrolled by.
                float4 uv : TEXCOORD0;
                half4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float2 cutoutUV : TEXCOORD1;
                float2 distortUV : TEXCOORD2;
                float2 scroll : TEXCOORD3;
                half4 color : COLOR;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            TEXTURE2D(_CutoutTex);
            SAMPLER(sampler_CutoutTex);
            TEXTURE2D(_DistTex);
            SAMPLER(sampler_DistTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _CutoutTex_ST;
                float4 _DistTex_ST;
                float4 _TintColor;
                float4 _DistStrength;
                float _UseAlphaCutout;
                float _Life;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = TRANSFORM_TEX(input.uv.xy, _MainTex);
                output.cutoutUV = TRANSFORM_TEX(input.uv.xy, _CutoutTex);
                output.distortUV = TRANSFORM_TEX(input.uv.xy, _DistTex);
                output.scroll = input.uv.zw;
                output.color = input.color;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                // Centred on zero rather than taken raw, because the pack read
                // the noise straight and so only ever pushed the sample one
                // way. That was harmless when a material used the whole sheet
                // - the shift just rolled the pattern around - but the band's
                // texture sits in a narrow window of it, and a one-sided push
                // walks the sample clean out of that window and off into the
                // empty margins. Centred, the smear ranges either side of
                // where the sample belongs and stays on the ink.
                float2 flow = input.distortUV + _Time.x * _DistStrength.zw;
                half2 noise =
                    SAMPLE_TEXTURE2D(_DistTex, sampler_DistTex, flow).rg - 0.5h;

                half2 distort = noise * _DistStrength.xy;

                half4 col = SAMPLE_TEXTURE2D(
                    _MainTex, sampler_MainTex, input.uv + input.scroll + distort);

                col = 2.0h * col * _TintColor * input.color;

                // How far this surface has faded, from whichever source is
                // driving it - a particle's own alpha, a renderer's _Life, or
                // both where both apply.
                half life = input.color.a * _Life;

#if defined(USE_ALPHA_CUTOUT)
                // The threshold is that fade, so a fading surface is an eroding
                // one rather than a translucent one.
                half cut = SAMPLE_TEXTURE2D(
                    _CutoutTex, sampler_CutoutTex, input.cutoutUV - input.scroll).r;

                col.a = saturate(col.a * 4.0h) * step(cut - life, col.a);
#endif

                return col;
            }
            ENDHLSL
        }
    }

    Fallback Off
}
