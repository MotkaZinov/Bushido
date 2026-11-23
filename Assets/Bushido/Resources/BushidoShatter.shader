Shader "Bushido/Shatter"
{
 // Overlay sprite for the judgment cut: screen pieces (frozen frame), cut-edge glow and the katana.
 // uv1.x = 1 paints the vertex colour instead of the texture (edge glow).
 Properties {
  _MainTex("Texture",2D)="white"{}
  _UseAlpha("Use texture alpha",Float)=0
 }
 SubShader {
 Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Overlay+20" }
 Pass { Cull Off ZWrite Off ZTest Always Blend SrcAlpha OneMinusSrcAlpha
 HLSLPROGRAM
 #pragma vertex Vert
 #pragma fragment Frag
 #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
 TEXTURE2D(_MainTex);SAMPLER(sampler_MainTex);
 CBUFFER_START(UnityPerMaterial)
 float4 _MainTex_ST; float _UseAlpha;
 CBUFFER_END
 struct A{float4 p:POSITION;float2 uv:TEXCOORD0;float2 glow:TEXCOORD1;float4 c:COLOR;};
 struct V{float4 p:SV_POSITION;float2 uv:TEXCOORD0;float glow:TEXCOORD1;float4 c:COLOR;};
 V Vert(A a){V o;o.p=TransformObjectToHClip(a.p.xyz);o.uv=a.uv;o.glow=a.glow.x;o.c=a.c;return o;}
 half4 Frag(V i):SV_Target{
  half4 t=SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,i.uv);
  float3 rgb=lerp(t.rgb*i.c.rgb,i.c.rgb,i.glow);
  // The paper weapon art has a faint halo: cut it out as the paper shader does (alpha .9).
  return half4(rgb,i.c.a*lerp(1,lerp(1,smoothstep(.85,.95,t.a),_UseAlpha),1-i.glow));
 }
 ENDHLSL
 } }
}
