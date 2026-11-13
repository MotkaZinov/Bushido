Shader "Bushido/SpiritSmoke"
{
 Properties { _Tint("Ink mist",Color)=(.57,.84,.72,.4) }
 SubShader {
 Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent" }
 Pass { Cull Off ZWrite Off Blend SrcAlpha OneMinusSrcAlpha
 HLSLPROGRAM
 #pragma vertex Vert
 #pragma fragment Frag
 #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
 CBUFFER_START(UnityPerMaterial)
 float4 _Tint;
 CBUFFER_END
 struct A{float4 p:POSITION;float2 uv:TEXCOORD0;};
 struct V{float4 p:SV_POSITION;float2 uv:TEXCOORD0;};
 V Vert(A a){V o;o.p=TransformObjectToHClip(a.p.xyz);o.uv=a.uv;return o;}
 half4 Frag(V i):SV_Target{
 float flow=i.uv.x*22-_Time.y*1.6;
 float edge=abs(i.uv.y*2-1);
 float fibers=.65+.2*sin(flow+sin(flow*1.7)*2)+.15*sin(flow*3.3+i.uv.y*9);
 float a=pow(saturate(1-edge),1.8)*fibers;
 a*=smoothstep(0,.08,i.uv.x)*smoothstep(0,.12,1-i.uv.x);
 return half4(_Tint.rgb,a*_Tint.a);
 }
 ENDHLSL
 } }
}
