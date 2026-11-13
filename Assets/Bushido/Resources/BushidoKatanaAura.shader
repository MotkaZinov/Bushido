Shader "Bushido/KatanaAura"
{
 Properties { _Power("Spirit intensity",Float)=0 _Age("Animation age",Float)=0 }
 SubShader {
  Tags {"RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent+5"}
  Pass {
   Cull Off ZWrite Off Blend One One
   HLSLPROGRAM
   #pragma vertex Vert
   #pragma fragment Frag
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   CBUFFER_START(UnityPerMaterial)
   float _Power, _Age;
   CBUFFER_END
   struct A {float4 p:POSITION;float2 uv:TEXCOORD0;};
   struct V {float4 p:SV_POSITION;float2 uv:TEXCOORD0;};
   V Vert(A a){V o;o.p=TransformObjectToHClip(a.p.xyz);o.uv=a.uv;return o;}
   half4 Frag(V i):SV_Target {
    float2 p=(i.uv-.5)*2;float r=length(p),angle=atan2(p.y,p.x);
    float breathing=.88+.12*sin(_Age*3.2);
    float halo=exp(-r*r*8)*.35;
    float core=exp(-r*r*140);
    float radius=.43+.045*sin(angle*3-_Age*2)+.025*sin(angle*7+_Age);
    float arc=exp(-pow((r-radius)*32,2))*pow(.5+.5*sin(angle*2-_Age*2.6),3);
    float flare=exp(-abs(p.y)*65-abs(p.x)*5)*.3;
    float edge=1-smoothstep(.72,1,r);
    float3 energy=float3(.04,.28,.85)*(halo+arc*.55)+float3(.35,.8,1)*(core+flare);
    return half4(energy*edge*breathing*_Power,0);
   }
   ENDHLSL
  }
 }
}
