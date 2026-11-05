Shader "Astra/Zyl/SubdermalCountdown"
{
 Properties
 {
  _Color("Tint",Color)=(1,1,1,1)
  _MainTex("Original albedo",2D)="white"{}
  _Glossiness("Smoothness",Range(0,1))=.2
  _Metallic("Metallic",Range(0,1))=0
  _BumpMap("Normal",2D)="bump"{}
  _BumpScale("Normal strength",Float)=1
  _EmissionColor("Base emission",Color)=(0,0,0,1)
  _CountdownWoundMask("Authored wound mask",2D)="black"{}
  _CountdownColor("Subdermal glow",Color)=(1,.12,.04,1)
  _CountdownDigits("MMSS",Vector)=(0,5,0,0)
  _CountdownVisible("Visible",Float)=0
  _CountdownBrightness("Brightness",Float)=2.5
  _CountdownWidth("Width",Float)=.12
  _CountdownHeight("Height",Float)=.032
  _CountdownHalo("Halo softness",Float)=.08
  _CountdownOffset("Display offset",Vector)=(0,0,0,0)
 }
 SubShader
 {
  Tags {"RenderType"="Opaque"}
  LOD 200
  CGPROGRAM
  #include "UnityStandardUtils.cginc"
  #pragma surface surf Standard fullforwardshadows addshadow vertex:CountdownVertex
  #pragma target 3.0
  #pragma shader_feature_local _NORMALMAP
  sampler2D _MainTex,_CountdownWoundMask,_BumpMap;
  fixed4 _Color,_EmissionColor,_CountdownColor;
  float4 _CountdownDigits,_CountdownOffset;
  float _Glossiness,_Metallic,_BumpScale,_CountdownVisible,_CountdownBrightness,_CountdownWidth,_CountdownHeight,_CountdownHalo;
  struct Input {float2 uv_MainTex;float3 countdownPosition;};
  void CountdownVertex(inout appdata_full v,out Input o){UNITY_INITIALIZE_OUTPUT(Input,o);o.countdownPosition=v.texcoord2.xyz;}
  float boxDistance(float2 p,float2 size){float2 q=abs(p)-size;return length(max(q,0))+min(max(q.x,q.y),0);}
  float bit(float value,float weight){return fmod(floor(value/weight),2);}
  float glyph(float2 p,float d)
  {
   float mask=d<.5?63:d<1.5?6:d<2.5?91:d<3.5?79:d<4.5?102:d<5.5?109:d<6.5?125:d<7.5?7:d<8.5?127:111;
   float dist=10;float2 bar=float2(.29,.045),vertical=float2(.045,.19);
   if(bit(mask,1)>.5)dist=min(dist,boxDistance(p-float2(0,.43),bar));
   if(bit(mask,2)>.5)dist=min(dist,boxDistance(p-float2(.32,.22),vertical));
   if(bit(mask,4)>.5)dist=min(dist,boxDistance(p-float2(.32,-.22),vertical));
   if(bit(mask,8)>.5)dist=min(dist,boxDistance(p-float2(0,-.43),bar));
   if(bit(mask,16)>.5)dist=min(dist,boxDistance(p-float2(-.32,-.22),vertical));
   if(bit(mask,32)>.5)dist=min(dist,boxDistance(p-float2(-.32,.22),vertical));
   if(bit(mask,64)>.5)dist=min(dist,boxDistance(p,bar));return dist;
  }
  float neutralSkinWeight(float3 color)
  {
   return (1-smoothstep(.04,.14,max(color.r,max(color.g,color.b))-min(color.r,min(color.g,color.b))))*step(.15,min(color.r,min(color.g,color.b)));
  }
  float3 removeChestPrinting(float2 uv,float3 original,float wound)
  {
   // Remove only warm printed ink inside the existing tear; keep surrounding texture intact.
   float ink=smoothstep(.005,.025,original.r-max(original.g,original.b))*wound;
   if(ink<.001)return original;
   float3 sum=0;float weight=0;
   const float2 offsets[12]={float2(.007,0),float2(-.007,0),float2(0,.007),float2(0,-.007),float2(.014,0),float2(-.014,0),float2(0,.014),float2(0,-.014),float2(.012,.012),float2(-.012,.012),float2(.012,-.012),float2(-.012,-.012)};
   [unroll]for(int i=0;i<12;i++){float3 sampleColor=tex2D(_MainTex,uv+offsets[i]).rgb*_Color.rgb;float w=neutralSkinWeight(sampleColor);sum+=sampleColor*w;weight+=w;}
   float3 clean=weight>.001?sum/max(.001,weight):original.rrr;
   return lerp(original,clean,ink);
  }
  void surf(Input IN,inout SurfaceOutputStandard o)
  {
   fixed4 original=tex2D(_MainTex,IN.uv_MainTex)*_Color;
   // Auxiliary rest coordinates span UV seams and move with the existing skinned vertices.
   float2 chest=float2(-IN.countdownPosition.x,IN.countdownPosition.y);
   float2 maskUV=(chest-float2(-.17,.69))/float2(.34,.21);
   float wound=tex2D(_CountdownWoundMask,maskUV).r*step(.015,IN.countdownPosition.z)*step(0,maskUV.x)*step(maskUV.x,1)*step(0,maskUV.y)*step(maskUV.y,1);
   float2 physical=chest-float2(0,.795)-_CountdownOffset.xy;
   float2 display=float2(physical.x/max(.001,_CountdownWidth),physical.y/max(.001,_CountdownHeight));
   float cell=floor((display.x+.5)*5);float2 p=float2(frac((display.x+.5)*5)-.5,display.y);
   float digit=cell<.5?_CountdownDigits.x:cell<1.5?_CountdownDigits.y:cell<3.5?_CountdownDigits.z:_CountdownDigits.w;
   float dist=cell>1.5&&cell<2.5?min(length(p-float2(0,.18)),length(p-float2(0,-.18)))-.055:glyph(p,digit);
   float inside=step(0,cell)*step(cell,4)*step(abs(display.y),.65);
   float aa=max(fwidth(dist),.006);float core=1-smoothstep(-aa-.006,aa+.012,dist);
   float halo=exp(-max(0,dist)*max(4,1/max(.01,_CountdownHalo)));
   float visibility=saturate(_CountdownVisible)*wound;
   float grain=sin(IN.uv_MainTex.x*83)*sin(IN.uv_MainTex.y*71);
   // Contrast follows the actual digit strokes, never a rectangular/solid backing patch.
   float printing=wound*step(abs(chest.x),.085)*step(abs(chest.y-.795),.030);
   float3 clean=removeChestPrinting(IN.uv_MainTex,original.rgb,printing);
   float3 pigment=pow(max(_CountdownColor.rgb,0),1.5);
   o.Albedo=lerp(clean,clean*pigment,core*inside*visibility*.9);
   o.Metallic=_Metallic;o.Smoothness=_Glossiness;o.Alpha=original.a;
   #ifdef _NORMALMAP
   o.Normal=UnpackScaleNormal(tex2D(_BumpMap,IN.uv_MainTex),_BumpScale);
   #endif
   float3 glow=pow(max(_CountdownColor.rgb,0),1.5);
   o.Emission=_EmissionColor.rgb+glow*_CountdownBrightness*(core*.60+halo*.12)*inside*visibility*(.97+grain*.03);
  }
  ENDCG
 }
 FallBack "Standard"
}


