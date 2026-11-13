Shader "Bushido/Ink"
{
    Properties
    {
        _BaseMap ("Paper", 2D) = "white" {}
        _BaseColor ("Ink", Color) = (1,1,1,1)
        _InkShading ("Ink Shading", Range(0,1)) = 1
        _CelShading ("Cel Shading", Range(0,1)) = 0
        _ForestLight ("Forest Light", Range(0,1)) = 0
        _StoneMapping ("Stone Mapping", Range(0,1)) = 0
        _CliffMap ("Cliff Rock", 2D) = "white" {}
        _TerrainBlend ("Terrain Blend", Range(0,1)) = 0
        _ClothWear ("Cloth Wear", Range(0,1)) = 0
        _LeafSurface ("Leaf Surface", Range(0,1)) = 0
        _VermilionWear ("Vermilion Wear", Range(0,1)) = 0
        _PaperAlphaCutoff ("Paper edge cutoff", Range(0,1)) = 0.1
        _PaperSurface ("Washi grain", Range(0,1)) = 0
        _MaskPaper ("Ofuda mask illustration", Range(0,1)) = 0
        _CharacterInk ("Illustrated character", Range(0,1)) = 0
        _SilhouetteInk ("Black silhouette", Range(0,1)) = 0
        _InkContour ("Ink contour strength", Range(0,1)) = 0.78
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Tags { "LightMode"="SRPDefaultUnlit" }
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);
            TEXTURE2D(_CliffMap);
            float4 _BushidoForestSun;
            float _BossFinalInk;
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
                half _InkShading;
                half _CelShading;
                half _ForestLight;
                half _StoneMapping;
                half _TerrainBlend;
                half _ClothWear;
                half _LeafSurface;
                half _VermilionWear;
                half _PaperSurface;
                half _PaperAlphaCutoff;
                half _CharacterInk;
                half _MaskPaper;
                half _SilhouetteInk;
                half _InkContour;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 normalWS : TEXCOORD0; float2 uv : TEXCOORD1; float3 positionWS : TEXCOORD2; };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                return output;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                half3 normal = normalize(input.normalWS);
                half light = dot(normal, normalize(float3(-0.4, 0.8, -0.5)));
                half shade = lerp(1.0h, 0.7h + 0.3h * saturate(light), _InkShading);
                half4 sampledSurface = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);
                if (_PaperSurface > 0.5h) clip(sampledSurface.a - _PaperAlphaCutoff);
                half3 surface = sampledSurface.rgb;
                half3 baseColor = surface * _BaseColor.rgb;
                if (_PaperSurface > 0.5h)
                {
                    float2 p = input.positionWS.xy;
                    half grain = frac(sin(dot(floor(p * 160), float2(127.1, 311.7))) * 43758.5453);
                    half fibers = sin(p.x * 83 + sin(p.y * 17) * 4) * sin(p.y * 117);
                    baseColor *= 0.94h + grain * 0.09h + fibers * 0.025h;
                }
                if (_LeafSurface > 0.5h)
                {
                    half detail = dot(surface, half3(0.21h, 0.72h, 0.07h));
                    baseColor = _BaseColor.rgb * (0.64h + detail * 0.85h);
                }
                if (_StoneMapping > 0.5h)
                {
                    float3 p = input.positionWS * 0.65;
                    half3 weights = abs(normal);
                    weights *= weights;
                    weights /= max(0.0001h, weights.x + weights.y + weights.z);
                    half3 stone = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, p.zy).rgb * weights.x
                        + SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, p.xz).rgb * weights.y
                        + SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, p.xy).rgb * weights.z;
                    if (_TerrainBlend > 0.5h)
                    {
                        half3 cliff = SAMPLE_TEXTURE2D(_CliffMap, sampler_BaseMap, p.zy).rgb * weights.x
                            + SAMPLE_TEXTURE2D(_CliffMap, sampler_BaseMap, p.xz).rgb * weights.y
                            + SAMPLE_TEXTURE2D(_CliffMap, sampler_BaseMap, p.xy).rgb * weights.z;
                        half erosion = sin(p.x * 3.1 + sin(p.z * 1.7)) * cos(p.z * 2.3) * 0.16h;
                        half exposure = smoothstep(0.13h, 0.50h, 1.0h - abs(normal.y) + erosion);
                        stone = lerp(stone, cliff, exposure);
                    }
                    baseColor = _BaseColor.rgb * (0.34h + stone * 1.65h);
                    if (_VermilionWear > 0.5h)
                    {
                        half grain = dot(stone, half3(0.21h, 0.72h, 0.07h));
                        half weathered = (1.0h - smoothstep(0.07h, 0.28h, grain))
                            * smoothstep(-0.1h, 0.82h, sin(p.y * 9.0 + sin(p.z * 12.0 + p.x * 4.0)));
                        baseColor = lerp(_BaseColor.rgb * (0.62h + grain * 0.86h),
                            half3(0.30h, 0.19h, 0.11h), weathered * 0.48h);
                    }
                }
                if (_ClothWear > 0.5h)
                {
                    float2 uv = input.uv;
                    half thread = sin(uv.x * 385.0) * sin(uv.y * 510.0);
                    half stain = smoothstep(-0.30h, 0.75h, sin(uv.x * 14.0 + sin(uv.y * 12.0)) * cos(uv.y * 23.0 - uv.x * 9.0));
                    half edgeWear = smoothstep(0.68h, 1.0h, uv.y) * (0.54h + 0.46h * sin(uv.x * 47.0));
                    baseColor *= 0.77h + thread * 0.08h - stain * 0.26h;
                    baseColor += half3(0.11h, 0.066h, 0.030h) * edgeWear;
                }
                half band = smoothstep(0.2h, 0.29h, light);
                half rim = 1.0h - saturate(dot(normal, GetWorldSpaceNormalizeViewDir(input.positionWS)));
                half3 ink = baseColor * lerp(half3(0.53h, 0.64h, 0.72h), half3(1.04h, 0.99h, 0.9h), band);
                ink += half3(0.16h, 0.22h, 0.25h) * smoothstep(0.71h, 0.95h, rim) * (0.45h + 0.55h * band);
                half3 color = lerp(baseColor * shade, ink, _CelShading);
                if (_ForestLight > 0.5h)
                {
                    float3 p = input.positionWS;
                    half3 sunDirection = normalize(_BushidoForestSun.xyz - p);
                    half sunlight = dot(normal, sunDirection);
                    half direct = saturate(sunlight);
                    direct = lerp(direct, smoothstep(0.04h, 0.33h, sunlight), _CelShading * 0.58h);
                    half clearing = 1.0h - smoothstep(1.10h, 3.65h, abs(p.x + 0.3h));
                    half canopy = smoothstep(0.13, 0.73, sin(p.x * 2.3 + p.z * 1.1) * cos(p.z * 1.64 - p.x * 0.7));
                    half gold = clearing * (0.22h + canopy * 0.88h);
                    half3 ambient = lerp(half3(0.22h, 0.27h, 0.23h), half3(0.39h, 0.40h, 0.31h), clearing);
                    half3 lit = baseColor * (ambient + half3(1.3h, 0.83h, 0.34h) * direct * gold);
                    half backlight = smoothstep(0.62h, 0.98h, rim) * gold;
                    lit += half3(0.21h, 0.12h, 0.048h) * backlight * max(_CelShading, _ClothWear);
                    lit += baseColor * half3(0.91h, 0.59h, 0.21h) * saturate(-sunlight) * gold * _LeafSurface;
                    half mist = smoothstep(4.5, 37, p.z) * lerp(0.51h, 0.70h, clearing);
                    color = lerp(lit, lerp(half3(0.085h, 0.11h, 0.088h), half3(0.69h, 0.46h, 0.22h), clearing), mist);
                }
                if (_CharacterInk > 0.5h)
                {
                    // Texture-locked grain stays attached to animated characters.
                    float2 paperUV = input.uv * 380;
                    half grain = frac(sin(dot(floor(paperUV), float2(127.1, 311.7))) * 43758.5453);
                    half luminance = dot(baseColor, half3(0.299h, 0.587h, 0.114h));
                    half3 pigment = saturate(lerp(luminance.xxx, baseColor, 1.08h));
                    half litBand = smoothstep(0.19h, 0.215h, light);
                    half highlightBand = smoothstep(0.68h, 0.70h, light);
                    half3 wash = pigment * lerp(half3(0.38h, 0.43h, 0.42h), half3(1.03h, 0.98h, 0.87h), litBand);
                    wash = lerp(wash, pigment * 0.9h + half3(0.10h, 0.08h, 0.045h), highlightBand * 0.3h);
                    wash *= 0.965h + grain * 0.07h;
                    // A narrow silhouette contour avoids extra geometry/draws on mobile.
                    half facing = abs(dot(normal, GetWorldSpaceNormalizeViewDir(input.positionWS)));
                    half contour = 1 - smoothstep(0.12h, 0.35h, facing);
                    half hatch = 1 - smoothstep(0.1h, 0.23h, abs(sin((input.uv.x + input.uv.y) * 260)));
                    wash *= 1 - hatch * (1 - litBand) * 0.18h;
                    half3 inkColor = half3(0.018h, 0.025h, 0.018h);
                    wash = lerp(wash, inkColor, contour * _InkContour);
                    color = lerp(wash, color, _ForestLight * 0.35h);
                }
                if (_MaskPaper > 0.5h)
                {
                    // Restricted washi/vermillion palette instead of glossy model shading.
                    half value=dot(baseColor,half3(.299h,.587h,.114h));
                    half red=saturate((baseColor.r-max(baseColor.g,baseColor.b))*4);
                    half3 paper=half3(.88h,.79h,.60h);
                    half3 pigment=lerp(paper,half3(.57h,.15h,.09h),red);
                    half dark=1-smoothstep(.08h,.28h,value);
                    pigment=lerp(pigment,half3(.045h,.055h,.038h),dark);
                    half facing=abs(dot(normal,GetWorldSpaceNormalizeViewDir(input.positionWS)));
                    half contour=1-smoothstep(.13h,.36h,facing);
                    half shadow=1-smoothstep(.1h,.2h,light);
                    float2 fibers=input.uv*360;
                    half grain=frac(sin(dot(floor(fibers),float2(127.1,311.7)))*43758.5453);
                    half fiber=sin(input.uv.x*730+sin(input.uv.y*91)*2)*sin(input.uv.y*960);
                    pigment*=.91h+grain*.15h+fiber*.025h;
                    pigment*=1-shadow*.10h;
                    // Warm, worn paper rim around the narrow brush contour.
                    half paperRim=1-smoothstep(.24h,.54h,facing);
                    pigment=lerp(pigment,paper*.93h,paperRim*.55h);
                    color=lerp(pigment,half3(.065h,.055h,.035h),contour*.86h);
                }
                if (_SilhouetteInk > 0.5h || (_BossFinalInk > 0.5 && _CharacterInk > 0.5h)) color = half3(0.015h, 0.02h, 0.015h);
                return half4(color, 1);
            }
            ENDHLSL
        }
    }
}


