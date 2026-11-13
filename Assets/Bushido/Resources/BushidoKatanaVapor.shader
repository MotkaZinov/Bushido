Shader "Bushido/KatanaVapor"
{
 Properties { _Age("Combat time",Float)=0 _Ember("Ember",Float)=0 }
 SubShader {
  Tags {"RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent+8"}
  Pass {
   Cull Off ZWrite Off Blend SrcAlpha OneMinusSrcAlpha
   HLSLPROGRAM
   #pragma vertex Vert
   #pragma fragment Frag
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   CBUFFER_START(UnityPerMaterial)
   float _Age, _Ember;
   CBUFFER_END
   struct A {float4 p:POSITION;float2 uv:TEXCOORD0;float4 color:COLOR;};
   struct V {float4 p:SV_POSITION;float2 uv:TEXCOORD0;float4 color:COLOR;float2 world:TEXCOORD1;};
   V Vert(A a){V o;o.p=TransformObjectToHClip(a.p.xyz);o.uv=a.uv;o.color=a.color;o.world=TransformObjectToWorld(a.p.xyz).xy;return o;}
   float hash(float2 p){return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453);}
   float noise(float2 p){float2 b=floor(p),f=frac(p);f=f*f*(3-2*f);return lerp(lerp(hash(b),hash(b+float2(1,0)),f.x),lerp(hash(b+float2(0,1)),hash(b+1),f.x),f.y);}
   float fbm(float2 p){return noise(p)*.55+noise(p*2.03+4.1)*.28+noise(p*4.07-3.7)*.17;}
   half4 Frag(V i):SV_Target {
    float2 p=i.uv*2-1;
    if(_Ember>.5){float r=dot(p,p);float star=exp(-r*8)+exp(-abs(p.x)*26-abs(p.y)*5)*.45;return half4(i.color.rgb,i.color.a*saturate(star)*(1-smoothstep(.65,1,r)));}
    float2 flow=i.world*1.8+float2(_Age*.16,-_Age*.32);
    float2 warp=float2(fbm(flow+2),fbm(flow-6))-.5;
    float r=length(p+warp*.36);
    float density=fbm(flow+warp*1.8);
    float edge=1-smoothstep(.25,1,r);
    float body=smoothstep(.2,.76,density)*edge*edge;
    float lit=pow(saturate(density),2)*edge;
    float3 color=lerp(i.color.rgb,float3(.24,.82,1.08),lit*.7);
    return half4(color,body*i.color.a);
   }
   ENDHLSL
  }
 }
}
