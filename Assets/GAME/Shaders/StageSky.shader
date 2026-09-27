Shader "Game/Stage Sky"
{
    // The backdrop for a showcase stage: three colours up a vertical axis and
    // nothing else. No sun, no clouds, no cubemap.
    //
    // That is the point rather than a shortcut. The stage exists to read a
    // character and an effect, and every feature a sky could have is another
    // thing competing for the eye - a cloud edge reads as a shape, a sun reads
    // as a light source the lighting rig has to agree with, a cubemap's detail
    // reads as somewhere rather than as background. A gradient reads as depth
    // and then gets out of the way.
    //
    // The horizon band is the brightest part and sits a little below eye level,
    // so the subject is framed against the darkest air rather than against the
    // brightest. Fog is set to this same horizon colour by the rig, which is
    // what makes the ground plane dissolve instead of ending at a visible edge.
    Properties
    {
        [HDR] _TopColor ("Zenith Colour", Color) = (0.055, 0.075, 0.105, 1)
        [HDR] _HorizonColor ("Horizon Colour", Color) = (0.50, 0.56, 0.62, 1)
        [HDR] _GroundColor ("Nadir Colour", Color) = (0.015, 0.02, 0.03, 1)

        _HorizonHeight ("Horizon Height", Range(-1, 1)) = 0.02
        _HorizonSoftness ("Horizon Softness", Range(0.01, 2)) = 0.55

        _TopFalloff ("Zenith Falloff", Range(0.1, 6)) = 1.1
        _GroundFalloff ("Nadir Falloff", Range(0.1, 6)) = 1.6

        _Exposure ("Exposure", Range(0, 4)) = 1
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Background"
            "RenderType" = "Background"
            "PreviewType" = "Skybox"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "StageSky"

            Cull Off
            ZWrite Off
            ZTest LEqual

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 direction : TEXCOORD0;
            };

            float4 _TopColor;
            float4 _HorizonColor;
            float4 _GroundColor;
            float _HorizonHeight;
            float _HorizonSoftness;
            float _TopFalloff;
            float _GroundFalloff;
            float _Exposure;

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);

                // A skybox mesh is drawn around the camera, so the object-space
                // position of a vertex is already the direction the eye is
                // looking through it.
                output.direction = input.positionOS.xyz;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float height = normalize(input.direction).y - _HorizonHeight;
                float softness = max(_HorizonSoftness, 1e-3);

                // Split either side of the horizon so the band can be tight
                // above and loose below, which is what stops the gradient
                // reading as a mirror through the middle of the screen.
                float up = pow(saturate(height / softness), _TopFalloff);
                float down = pow(saturate(-height / softness), _GroundFalloff);

                float3 colour = _HorizonColor.rgb;
                colour = lerp(colour, _TopColor.rgb, up);
                colour = lerp(colour, _GroundColor.rgb, down);

                return half4(colour * _Exposure, 1);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
