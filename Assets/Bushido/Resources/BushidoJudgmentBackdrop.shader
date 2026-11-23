Shader "Bushido/JudgmentBackdrop"
{
 // Full-screen quad behind the judgment cut: night wash, eclipse ring, white flash.
 Properties {
  _Dim("Dim 0-1",Float)=0
  _Ring("Ring 0-1",Float)=0
  _Flash("Flash 0-1",Float)=0
  _Center("Ring centre (uv)",Vector)=(.62,.74,0,0)
  _Aspect("Aspect",Float)=1.78
 }
 SubShader {
 Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Overlay" }
 Pass { Cull Off ZWrite Off ZTest Always Blend SrcAlpha OneMinusSrcAlpha
 HLSLPROGRAM
 #pragma vertex Vert
 #pragma fragment Frag
 #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
 CBUFFER_START(UnityPerMaterial)
 float4 _Center; float _Dim,_Ring,_Flash,_Aspect;
 CBUFFER_END
 struct A{float4 p:POSITION;float2 uv:TEXCOORD0;};
 struct V{float4 p:SV_POSITION;float2 uv:TEXCOORD0;};
 V Vert(A a){V o;o.p=TransformObjectToHClip(a.p.xyz);o.uv=a.uv;return o;}
 half4 Frag(V i):SV_Target{
  float2 p=(i.uv-_Center.xy)*float2(_Aspect,1);
  float d=length(p);
  float radius=.15;
  // The eclipse is an ensō: one brush turn of spirit light, loaded where it starts and dry where it ends.
  float turn=frac(atan2(p.y,p.x)/6.2832+.36);
  float ring=exp(-pow((d-radius)/lerp(.016,.005,turn),2))*smoothstep(1,.9,turn)*_Ring;
  float disc=smoothstep(radius,radius-.02,d)*_Ring;
  float2 q=p-float2(-.1,.112);                                   // spirit light peeking past the rim
  float glare=(exp(-dot(q,q)/.0012)+.02*exp(-dot(q,q)/.01))*_Ring;
  float3 night=lerp(float3(.011,.019,.019),float3(.02,.032,.032),i.uv.y*.6); // ink wash in the Ink role colour
  float3 color=night*(1-disc*.9)+float3(.55,.92,.72)*ring+glare*float3(.55,.95,.75);
  float alpha=saturate(_Dim*.985+ring+glare);
  color=lerp(color,float3(.95,.92,.82),_Flash);
  return half4(color,saturate(alpha+_Flash));
 }
 ENDHLSL
 } }
}
