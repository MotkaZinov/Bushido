Shader "Bushido/TrailGlow"
{
 Properties { _Tint("Color",Color)=(1,1,1,1) }
 SubShader
 {
  Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
  Pass
  {
   Tags { "LightMode"="SRPDefaultUnlit" }
   Blend SrcAlpha One
   ZWrite Off
   Cull Off
   HLSLPROGRAM
   #pragma vertex Vert
   #pragma fragment Frag
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   CBUFFER_START(UnityPerMaterial)
    half4 _Tint;
   CBUFFER_END
   struct A { float4 positionOS:POSITION;float2 uv:TEXCOORD0;half4 color:COLOR; };
   struct V { float4 positionCS:SV_POSITION;float2 uv:TEXCOORD0;half4 color:COLOR; };
   V Vert(A a){V o;o.positionCS=TransformObjectToHClip(a.positionOS.xyz);o.uv=a.uv;o.color=a.color;return o;}
   half4 Frag(V i):SV_Target
   {
    half edge=pow(saturate(1-abs(i.uv.y*2-1)),2);
    return half4(_Tint.rgb,edge*.42h*i.color.a);
   }
   ENDHLSL
  }
 }
}
