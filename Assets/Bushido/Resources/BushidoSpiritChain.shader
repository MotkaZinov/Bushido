Shader "Bushido/SpiritChain"
{
 Properties { _BaseMap("Links",2D)="white"{} _Tint("Spirit",Color)=(.5,1,.82,1) _Pulse("Binding pulse",Range(0,1))=0 }
 SubShader {
 Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="TransparentCutout" "Queue"="AlphaTest" }
 Pass { Cull Off
 HLSLPROGRAM
 #pragma vertex Vert
 #pragma fragment Frag
 #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
 TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
 CBUFFER_START(UnityPerMaterial)
 float4 _Tint; float _Pulse;
 CBUFFER_END
 struct A {float4 p:POSITION;float2 uv:TEXCOORD0;};
 struct V {float4 p:SV_POSITION;float2 uv:TEXCOORD0;};
 V Vert(A a){V o;o.p=TransformObjectToHClip(a.p.xyz);o.uv=a.uv;return o;}
 half4 Frag(V i):SV_Target{
 half4 s=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,i.uv);clip(s.a-.25);
 half tone=dot(s.rgb,half3(.299,.587,.114));
 half3 col=lerp(half3(.035,.075,.065),_Tint.rgb,saturate(tone*1.7+.12));
 col=lerp(col,half3(.9,1,.82),_Pulse*.55);return half4(col,1);
 }
 ENDHLSL
 } }
}
