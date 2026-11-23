Shader "Bushido/Dusk"
{
 // One full-screen veil behind the gameplay: ink-dark dusk with ragged watercolour pools of warm light (up to 8, viewport space).
 Properties { _Tint("Dusk ink",Color)=(.13,.17,.17,1) _Warm("Lamp light",Color)=(.82,.62,.28,1) _Dark("Dusk alpha",Range(0,1))=.6 _Warmth("Warm wash",Range(0,.4))=.06 _Aspect("Aspect",Float)=2 }
 SubShader {
 Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent" }
 Pass { Cull Off ZWrite Off Blend SrcAlpha OneMinusSrcAlpha
 HLSLPROGRAM
 #pragma vertex Vert
 #pragma fragment Frag
 #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
 CBUFFER_START(UnityPerMaterial)
 float4 _Tint; float4 _Warm; float _Dark; float _Warmth; float _Aspect;
 CBUFFER_END
 float4 _Lights[8]; // x,y viewport; z radius (viewport heights); w strength
 struct A{float4 p:POSITION;float2 uv:TEXCOORD0;};
 struct V{float4 p:SV_POSITION;float2 uv:TEXCOORD0;};
 V Vert(A a){V o;o.p=TransformObjectToHClip(a.p.xyz);o.uv=a.uv;return o;}
 float Hash(float2 p){return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453);}
 float Noise(float2 p){float2 i=floor(p),f=frac(p);f=f*f*(3-2*f);
  return lerp(lerp(Hash(i),Hash(i+float2(1,0)),f.x),lerp(Hash(i+float2(0,1)),Hash(i+1),f.x),f.y);}
 half4 Frag(V i):SV_Target{
  float2 p=i.uv*float2(_Aspect,1);
  float n=Noise(p*5+_Time.y*.08)*.65+Noise(p*13-_Time.y*.05)*.35; // ragged, slowly drifting edge
  float k=0,rim=0;
  [unroll] for(int j=0;j<8;j++){
   float4 L=_Lights[j]; L.z=max(L.z,1e-3);
   float e=length(p-L.xy*float2(_Aspect,1))+(n-.5)*L.z*.7;
   float core=1-smoothstep(L.z*.5,L.z,e), wash=1-smoothstep(L.z,L.z*1.5,e); // two washes of the same pool
   k=max(k,(core*.65+wash*.35)*L.w);
   rim=max(rim,smoothstep(L.z*.9,L.z*1.02,e)*(1-smoothstep(L.z*1.02,L.z*1.2,e))*L.w); // tide mark where the wash dried
  }
  float dark=_Dark*(1-k)+rim*_Dark*.35;
  float a=lerp(dark,_Warmth,saturate(k));
  float3 c=lerp(_Tint.rgb,_Warm.rgb,saturate(k));
  return half4(c,saturate(a));
 }
 ENDHLSL
 } }
}
