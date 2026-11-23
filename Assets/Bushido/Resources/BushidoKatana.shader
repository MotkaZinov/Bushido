Shader "Bushido/Katana"
{
 // Katana y funda 3D (Tools/katana_import.py): color del atlas (uv1), oclusión horneada (uv0.x),
 // alfa de vértice 1 = acero de la hoja, la única parte por la que corre el espíritu.
 Properties {
  _BaseMap("Weapon atlas",2D)="white"{}
  _BladeBottom("Visible blade limit",Float)=-10
  _Age("Presentation time",Float)=0
  _Spirit("Spirit reflection",Float)=0
  _Energy("Blade energy 0-1",Float)=0
  _Taper("Fake perspective toward the tip",Float)=0
  _Flat("Flatten depth 0-1",Float)=0
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
   float4 _BaseMap_ST; float _BladeBottom; float _Age; float _Spirit; float _Energy; float _Taper; float _Flat;
   CBUFFER_END
   struct A {float4 p:POSITION;float3 n:NORMAL;float4 c:COLOR;float2 shade:TEXCOORD0;float2 uv:TEXCOORD1;};
   struct V {float4 p:SV_POSITION;float3 n:TEXCOORD0;float4 c:COLOR;float3 local:TEXCOORD2;float3 world:TEXCOORD3;float2 shade:TEXCOORD1;float2 uv:TEXCOORD4;};
   V Vert(A a){V o;
    // The camera is orthographic: _Taper narrows the weapon toward its tip (grip y=.9, tip y=-1.15) to fake perspective,
    // and _Flat keeps a weapon tilted into the screen from sinking behind the scenery or through the near plane.
    a.p.xz/=1+_Taper*saturate((.9-a.p.y)/2.05);
    float3 viewPos=TransformWorldToView(TransformObjectToWorld(a.p.xyz));
    viewPos.z=lerp(viewPos.z,-1,_Flat);
    o.p=TransformWViewToHClip(viewPos);o.n=TransformObjectToWorldNormal(a.n);o.c=a.c;o.local=a.p.xyz;o.world=TransformObjectToWorld(a.p.xyz);o.shade=a.shade;o.uv=a.uv;return o;}
   half4 Frag(V i):SV_Target {
    clip(i.local.y-_BladeBottom);
    float3 n=normalize(i.n),v=GetWorldSpaceNormalizeViewDir(i.world);
    float light=.48+.52*abs(dot(n,normalize(float3(-.5,.8,-1))));
    float rim=pow(1-saturate(abs(dot(n,v))),3);
    float glint=pow(saturate(abs(dot(n,normalize(float3(sin(_Age*.7)*.4,.7,-1))))),28);
    // Painted look: baked occlusion darkens and cools the cavities.
    float3 base=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,i.uv).rgb*lerp(float3(.32,.34,.33),1,smoothstep(.15,.9,i.shade.x));
    float edge=exp(-abs(i.local.y-_BladeBottom)*38)*step(-1,_BladeBottom);
    float steel=step(.75,i.c.a);
    float broad=pow(saturate(abs(dot(n,normalize(float3(-.65,.3,-1))))),8);
    float reflection=exp(-abs(i.local.y-.43)*5)*_Spirit;
    // Spirit runs down the bare blade like pale light through wet ink: slow veins, a few bright threads.
    float flow=.55+.45*sin(i.local.y*16+_Age*8+sin(i.local.x*60+_Age*3)*1.5);
    float3 energy=(float3(.22,.6,.4)*(.3+flow*.6)+float3(.85,.98,.9)*pow(flow,6)*.8)*steel*smoothstep(.44,.38,i.local.y)*_Energy;
    return half4(base*light+broad*steel*.18+float3(.45,.8,.62)*rim*(.12+reflection*.5)+glint*(.08+steel*.35)+float3(.5,.9,.7)*edge*_Spirit*.7+float3(.08,.22,.14)*reflection+energy,1);
   }
   ENDHLSL
  }
 }
}
