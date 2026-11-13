Shader "Bushido/ForestSunlight"
{
    Properties
    {
        [HDR] _Color ("Color", Color) = (1, 0.68, 0.32, 1)
        _Strength ("Strength", Range(0, 1)) = 0.2
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Tags { "LightMode"="SRPDefaultUnlit" }
            Blend One One
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half _Strength;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 uv = input.uv;
                half across = 1.0h - smoothstep(0.08h, 0.5h, abs(uv.x - 0.5h));
                half along = smoothstep(0.0h, 0.15h, uv.y) * (1.0h - smoothstep(0.25h, 1.0h, uv.y));
                half flicker = 0.86h + 0.14h * sin(_Time.y * 0.72h + uv.y * 4.0h);
                half gaps = 0.67h + 0.33h * smoothstep(-0.4h, 0.6h, sin(uv.x * 18.0h + uv.y * 1.7h));
                half glow = across * along * flicker * gaps;
                return half4(_Color.rgb * glow * _Strength, 0);
            }
            ENDHLSL
        }
    }
}
