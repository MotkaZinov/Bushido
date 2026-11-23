Shader "Bushido/KatanaGhost"
{
 // Afterimage of the katana: an additive silhouette of spirit light, brightest on its rim and on the blade steel.
 Properties {
  _Tint("Tint (alpha = strength)",Color)=(.42,.8,.6,.5)
  _Taper("Fake perspective toward the tip",Float)=0
  _Flat("Flatten depth 0-1",Float)=0
 }
 SubShader {
 Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Overlay+20" }
 Pass { Cull Off ZWrite Off ZTest Always Blend One One
 HLSLPROGRAM
 #pragma vertex Vert
 #pragma fragment Frag
 #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
 // Not in UnityPerMaterial: each afterimage sets its own through a MaterialPropertyBlock.
 float4 _Tint; float _Taper,_Flat;
 struct A{float4 p:POSITION;float3 n:NORMAL;float4 c:COLOR;};
 struct V{float4 p:SV_POSITION;float3 n:TEXCOORD0;float3 world:TEXCOORD1;float steel:TEXCOORD2;};
 V Vert(A a){V o;
  // Same taper and depth flattening as BushidoKatana, so the afterimage overlays the blade exactly.
  a.p.xz/=1+_Taper*saturate((.9-a.p.y)/2.05);
  o.world=TransformObjectToWorld(a.p.xyz);
  float3 viewPos=TransformWorldToView(o.world);
  viewPos.z=lerp(viewPos.z,-1,_Flat);
  o.p=TransformWViewToHClip(viewPos);o.n=TransformObjectToWorldNormal(a.n);o.steel=step(.75,a.c.a);return o;}
 half4 Frag(V i):SV_Target{
  float rim=pow(1-saturate(abs(dot(normalize(i.n),GetWorldSpaceNormalizeViewDir(i.world)))),2);
  return half4(_Tint.rgb*(.3+rim)*lerp(.3,1,i.steel)*_Tint.a,0);
 }
 ENDHLSL
 } }
}
