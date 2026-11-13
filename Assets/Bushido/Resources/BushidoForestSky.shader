Shader "Bushido/ForestSky"
{
    Properties
    {
        _SunPosition ("Sun Position", Vector) = (-2.2, 12.4, 48, 1)
        _CloudSamples ("Cloud Samples", Integer) = 12
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Geometry+490" "RenderType"="Opaque" }
        Pass
        {
            Tags { "LightMode"="SRPDefaultUnlit" }
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 3.5
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _SunPosition;
                int _CloudSamples;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                return output;
            }

            float Hash(float3 p)
            {
                p = frac(p * 0.1031);
                p += dot(p, p.yzx + 33.33);
                return frac((p.x + p.y) * p.z);
            }

            float Noise(float3 p)
            {
                float3 cell = floor(p);
                float3 w = frac(p);
                w = w * w * (3.0 - 2.0 * w);
                float lower = lerp(lerp(Hash(cell), Hash(cell + float3(1,0,0)), w.x),
                    lerp(Hash(cell + float3(0,1,0)), Hash(cell + float3(1,1,0)), w.x), w.y);
                float upper = lerp(lerp(Hash(cell + float3(0,0,1)), Hash(cell + float3(1,0,1)), w.x),
                    lerp(Hash(cell + float3(0,1,1)), Hash(cell + float3(1,1,1)), w.x), w.y);
                return lerp(lower, upper, w.z);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float3 camera = GetCameraPositionWS();
                float3 direction = normalize(input.positionWS - camera);
                float3 sunDir = normalize(_SunPosition.xyz - camera);
                float alignment = saturate(dot(direction, sunDir));
                float lateral = abs(direction.x / max(0.1, direction.z) - sunDir.x / sunDir.z);
                half opening = 1.0h - smoothstep(0.065, 0.29, lateral);
                const half3 forestShade = half3(0.072h, 0.092h, 0.078h);
                if (opening <= 0.001h) return half4(forestShade, 1);
                half3 sky = lerp(half3(0.47h, 0.35h, 0.21h), half3(0.12h, 0.19h, 0.24h),
                    smoothstep(0.02, 0.56, direction.y));
                half aureole = pow(alignment, 54.0);
                half clearWindow = smoothstep(0.90h, 0.998h, alignment);
                sky += half3(1.47h, 0.72h, 0.25h) * aureole * 0.73h;
                sky += half3(3.1h, 1.95h, 0.82h) * smoothstep(0.9977, 0.9992, alignment);
                if (direction.y <= 0.07) return half4(lerp(forestShade, sky, opening), 1);

                float entry = max(0.0, (10.0 - camera.y) / direction.y);
                float rayEnd = min(123.0, (21.0 - camera.y) / direction.y);
                if (rayEnd <= entry) return half4(lerp(forestShade, sky, opening), 1);

                int samples = clamp(_CloudSamples, 1, 12);
                float stride = (rayEnd - entry) / samples;
                float offset = Hash(float3(input.positionCS.xy, 0.0));
                half transmittance = 1.0h;
                half3 clouds = 0.0h;
                half cloudGlow = pow(alignment, 18.0) * 0.36h;
                [loop]
                for (int sampleIndex = 0; sampleIndex < samples; sampleIndex++)
                {
                    float3 cloudPosition = camera + direction * (entry + (sampleIndex + offset) * stride);
                    float altitude = saturate((cloudPosition.y - 10.0) / 11.0);
                    float3 cell = cloudPosition * float3(0.072, 0.17, 0.065) + float3(_Time.y * 0.032, 0, _Time.y * 0.014);
                    float field = Noise(cell);
                    if (samples > 5) field = field * 0.76 + Noise(cell * 2.23 + 5.0) * 0.24;
                    half density = smoothstep(0.51, 0.79, field)
                        * smoothstep(0.0, 0.28, altitude) * (1.0 - smoothstep(0.64, 1.0, altitude));
                    density *= 1.0h - clearWindow * 0.94h;
                    half opacity = 1.0h - exp(-density * stride * 0.18h);
                    half glow = saturate(altitude * 0.78h + cloudGlow);
                    half3 scatter = lerp(half3(0.16h, 0.21h, 0.25h), half3(1.02h, 0.66h, 0.32h), glow);
                    scatter += half3(0.62h, 0.32h, 0.095h) * aureole * (1.0h - density);
                    clouds += scatter * (opacity * transmittance);
                    transmittance *= 1.0h - opacity;
                    if (transmittance < 0.04h) break;
                }
                return half4(lerp(forestShade, clouds + sky * transmittance, opening), 1);
            }
            ENDHLSL
        }
    }
}
