Shader "Bushido/Katana"
{
 Properties {
  _BaseMap("Original katana texture",2D)="white"{}
  _BladeBottom("Visible blade limit",Float)=-10
  _Age("Presentation time",Float)=0
  _Spirit("Spirit reflection",Float)=0
 }
 SubShader {
  Tags {"RenderPipeline"="UniversalPipeline" "RenderType"="Opaque"}
  Pass {
   Tags {"LightMode"="SRPDefaultUnlit"}
   Cull Off
   HLSLPROGRAM
   #pragma vertex Vert
   #pragma fragment Frag
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
   CBUFFER_START(UnityPerMaterial)
   float4 _BaseMap_ST; float _BladeBottom; float _Age; float _Spirit;
   CBUFFER_END
   struct A {float4 p:POSITION;float3 n:NORMAL;float2 uv:TEXCOORD0;};
   struct V {float4 p:SV_POSITION;float3 n:TEXCOORD0;float2 uv:TEXCOORD1;float3 local:TEXCOORD2;float3 world:TEXCOORD3;};
   V Vert(A a){V o;o.p=TransformObjectToHClip(a.p.xyz);o.n=TransformObjectToWorldNormal(a.n);o.uv=TRANSFORM_TEX(a.uv,_BaseMap);o.local=a.p.xyz;o.world=TransformObjectToWorld(a.p.xyz);return o;}
   half4 Frag(V i):SV_Target {
    clip(i.local.y-_BladeBottom);
    float3 n=normalize(i.n),v=GetWorldSpaceNormalizeViewDir(i.world);
    float light=.48+.52*abs(dot(n,normalize(float3(-.5,.8,-1))));
    float rim=pow(1-saturate(abs(dot(n,v))),3);
    float glint=pow(saturate(abs(dot(n,normalize(float3(sin(_Age*.7)*.4,.7,-1))))),28);
    float3 base=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,i.uv).rgb;
    float edge=exp(-abs(i.local.y-_BladeBottom)*38)*step(-1,_BladeBottom);
    float steel=smoothstep(.16,.7,dot(base,float3(.2126,.7152,.0722)));
    float broad=pow(saturate(abs(dot(n,normalize(float3(-.65,.3,-1))))),8);
    float reflection=exp(-abs(i.local.y-.43)*5)*_Spirit;
    return half4(base*light+broad*steel*.18+float3(.15,.5,.8)*rim*(.15+reflection*.5)+glint*(.08+steel*.35)+float3(.12,.65,1)*edge*_Spirit*.7+float3(.04,.27,.5)*reflection,1);
   }
   ENDHLSL
  }
 }
}
