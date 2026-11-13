Shader "Bushido/KatanaFilament"
{
 Properties { _Age("Combat time",Float)=0 _Power("Energy",Float)=0 }
 SubShader {
  Tags {"RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent+10"}
  Pass {
   Cull Off ZWrite Off Blend One One
   HLSLPROGRAM
   #pragma vertex Vert
   #pragma fragment Frag
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   CBUFFER_START(UnityPerMaterial)
   float _Age,_Power;
   CBUFFER_END
   struct A {float4 p:POSITION;float2 uv:TEXCOORD0;};
   struct V {float4 p:SV_POSITION;float2 uv:TEXCOORD0;};
   V Vert(A a){V o;o.p=TransformObjectToHClip(a.p.xyz);o.uv=a.uv;return o;}
   half4 Frag(V i):SV_Target {
    float edge=exp(-pow((i.uv.y*2-1)*2.4,2));
    float taper=sin(saturate(i.uv.x)*PI);
    float pulse=.55+.45*pow(.5+.5*sin(i.uv.x*9-_Age*3),3);
    return half4(float3(.12,.82,1.65)*edge*taper*pulse*_Power,0);
   }
   ENDHLSL
  }
 }
}
