Shader "Bushido/VfxEnergy"
{
 // Strokes for Models/Vfx strips and the blade trail: uv0 = (along, across), uv1 = position.
 // _Ink 1: a sumi brush stroke (ink body with dry bristles, a white cut down the middle, spirit light on its rim).
 // _Ink 0: pure spirit light (the guard glint).
 Properties {
  _Tint("Spirit tint",Color)=(.55,.92,.72,1)
  _Reveal("Head 0-1",Float)=1
  _Tail("Tail 0-1",Float)=0
  _Sharp("Core sharpness",Float)=3
  _Streak("Streaks 0-1",Float)=0
  _Radial("Radial fade 0-1",Float)=0
  _White("White core 0-1",Float)=1
  _Ink("Ink stroke 0-1",Float)=1
 }
 SubShader {
 Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Overlay+10" }
 Pass { Cull Off ZWrite Off ZTest Always Blend SrcAlpha OneMinusSrcAlpha
 HLSLPROGRAM
 #pragma vertex Vert
 #pragma fragment Frag
 #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
 // Not in UnityPerMaterial: each slash sets these through a MaterialPropertyBlock.
 float4 _Tint; float _Reveal,_Tail,_Sharp,_Streak,_Radial,_White,_Ink;
 struct A{float4 p:POSITION;float2 uv:TEXCOORD0;float2 pos:TEXCOORD1;};
 struct V{float4 p:SV_POSITION;float2 uv:TEXCOORD0;};
 V Vert(A a){V o;o.p=TransformObjectToHClip(float3(a.pos,0));o.uv=a.uv;return o;}
 half4 Frag(V i):SV_Target{
  float u=i.uv.x;
  float across=saturate(1-abs(i.uv.y*2-1));                    // 0 at the rim, 1 on the centre line
  float fade=smoothstep(_Reveal,_Reveal-.06,u)*smoothstep(_Tail-.06,_Tail,u)*lerp(1,(1-u)*(1-u),_Radial);
  // Spirit light
  float core=pow(across,_Sharp);
  float flow=i.uv.y*24+u*7-_Time.y*14;                         // veins run along the blade
  float fibers=lerp(1,.6+.25*sin(flow)+.15*sin(flow*2.3+u*31),_Streak);
  float3 light=lerp(_Tint.rgb,1,saturate(core*core*1.4-.25)*_White);
  float lightAlpha=core*fibers;
  // Ink stroke: each bristle carries its own load, so the rim and the tail break into dry lines.
  float bristle=frac(sin(floor(i.uv.y*54)*91.7)*43758.5453);
  float dry=smoothstep(.2,.6,bristle*.9+across-u*.7);
  float body=smoothstep(.06,.2,across)*dry;
  float cut=smoothstep(.94,.985,across);
  float halo=smoothstep(0,.06,across)*(1-smoothstep(.06,.2,across));
  float3 ink=lerp(lerp(_Tint.rgb,float3(.011,.019,.019),body),1,cut*_White);
  float inkAlpha=max(body,halo*.6);
  return half4(lerp(light,ink,_Ink),saturate(lerp(lightAlpha,inkAlpha,_Ink)*fade*_Tint.a));
 }
 ENDHLSL
 } }
}
