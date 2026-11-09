Shader "Astra/Zyl/FoolsMucosa" {
 Properties {
 [HideInInspector] _CutOpen ("Opening",Float)=0
 [HideInInspector] _Poke ("Impact",Float)=0
 [HideInInspector] _CutCenter ("Cut center",Vector)=(0,0,0,0)
 [HideInInspector] _PoleAxis ("Pole axis",Vector)=(0,1,0,0)
 [HideInInspector] _CutNormal ("Cut normal",Vector)=(0,1,0,0)
 [HideInInspector] _CutRight ("Cut right",Vector)=(1,0,0,0)
 [HideInInspector] _CutUp ("Cut up",Vector)=(0,0,1,0)
 _Color ("Tint",Color)=(1,1,1,1)
 _MainTex ("Mucosa albedo",2D)="white"{}
 _BumpMap ("Fine membrane normal",2D)="bump"{}
 _BumpScale ("Normal strength",Range(0,1))=.15
 _SpecColor ("Specular",Color)=(.055,.055,.055,1)
 _Glossiness ("Smoothness",Range(0,1))=.52
 _BreathAmplitude ("Breath amplitude (world units)",Range(0,2))=.6
 _BreathPeriod ("Breath period (seconds)",Range(.5,20))=4.5
 [HideInInspector] _UseBreathFrameMatrix ("Runtime frame matrix",Float)=0
 _BreathFrameOffset ("Authored breathing frame offset",Vector)=(0,0,0,0)
 _BreathFrameYaw ("Authored breathing frame yaw",Float)=0
 [HideInInspector] _BreathPreviewTime ("QA time override; -1 realtime",Float)=-1
 }
 SubShader {
 Tags {"RenderType"="Opaque"} LOD 300
 CGPROGRAM
 #pragma surface surf StandardSpecular fullforwardshadows vertex:vert addshadow
 #pragma target 3.0
 #include "UnityCG.cginc"
 sampler2D _MainTex,_BumpMap;fixed4 _Color;half _BumpScale,_Glossiness;
 float _CutOpen,_Poke;float4 _CutCenter,_CutNormal,_CutRight,_CutUp,_PoleAxis;
 float _BreathAmplitude,_BreathPeriod,_BreathPreviewTime;
 float _UseBreathFrameMatrix;float4x4 _BreathWorldToAuth,_BreathAuthToWorld;
 float4 _BreathFrameOffset;float _BreathFrameYaw;
 float3 RotateFrame(float3 p,float angle){float sn,cs;sincos(radians(angle),sn,cs);return float3(cs*p.x+sn*p.z,p.y,-sn*p.x+cs*p.z);}
 struct Input {float2 uv_MainTex;float2 uv_BumpMap;float3 cut;};
 float2 SplitCoordinates(float3 q)
 {
  float x=q.x;
  float center=.095*sin(x*4.7)+.037*sin(x*11.2+1);
  float width=max(.01,1.65*sqrt(_CutOpen));
  float halfGap=max(.01,(.08+.26*_CutOpen)*_CutOpen);
  halfGap*=1+.21*sin(x*7.5)+.12*sin(x*15.9+.8);
  return float2(x/width,(q.y-center*_CutOpen)/halfGap);
 }
 float WallDepth(float3 q)
 {
  float local=1-smoothstep(1.25,2.35,abs(q.z));
  float dent=saturate(1-dot(q.xy/float2(1.80,1.30),q.xy/float2(1.80,1.30)));
  float crack=length(SplitCoordinates(q));
  // Fold the original wall back into a narrow fissure. The floor remains part of the wall.
  float recess=1-smoothstep(.48,1.12,crack);
  float rim=smoothstep(.82,1.02,crack)*(1-smoothstep(1.02,1.45,crack));
  return local*(_Poke*dent*dent+_CutOpen*(1.02*recess-.10*rim));
 }
 void vert(inout appdata_full v, out Input data) {
  UNITY_INITIALIZE_OUTPUT(Input,data);
  float3 world=mul(unity_ObjectToWorld,v.vertex).xyz;
  float3 p=_UseBreathFrameMatrix>.5?mul(_BreathWorldToAuth,float4(world,1)).xyz:RotateFrame(world,_BreathFrameYaw)+_BreathFrameOffset.xyz;
  float time=_BreathPreviewTime>=0?_BreathPreviewTime:_Time.y;
  // Anchor the submerged wall and both end closures. Broad spatial masks select partial patches.
  float heightMask=smoothstep(1.5,7.0,p.y);
  float endMask=smoothstep(-260,-215,p.z)*(1-smoothstep(325,370,p.z));
  float patch=smoothstep(-.25,.65,sin(p.z*.027+p.x*.055));
  float phase=6.2831853*time/max(.5,_BreathPeriod)+p.z*.008;
  float pulse=sin(phase)*.85+sin(phase*2+.4)*.15;
  float3 radial=normalize(float3(p.x,p.y-6,0)+float3(.0001,0,0));
  float3 worldOffset=radial*(_BreathAmplitude*pulse*heightMask*endMask*patch);
  float3 offset=_UseBreathFrameMatrix>.5?mul((float3x3)_BreathAuthToWorld,worldOffset):RotateFrame(worldOffset,-_BreathFrameYaw);
  float3 delta=world-_CutCenter.xyz;
  float3 q=float3(dot(delta,_CutRight.xyz),dot(delta,_CutUp.xyz),dot(delta,_CutNormal.xyz));
  data.cut=q;
  if (abs(q.z)>=2.35 || dot(q.xy,q.xy)>=5.5225) {
   v.vertex.xyz+=mul((float3x3)unity_WorldToObject,offset);
   return;
  }
  float anchor=(1-smoothstep(.12,2.35,length(q.xy)))*(1-smoothstep(1.25,2.35,abs(q.z)));
  offset*=1-anchor;

  float depth=WallDepth(q);
  offset+=_PoleAxis.xyz*depth;
  // Differentiate the same displacement used by the vertices for stable highlights.
  const float step=.028;
  float dx=(WallDepth(q+float3(step,0,0))-WallDepth(q-float3(step,0,0)))/(2*step);
  float dy=(WallDepth(q+float3(0,step,0))-WallDepth(q-float3(0,step,0)))/(2*step);
  float3 tangentX=_CutRight.xyz+_PoleAxis.xyz*dx;
  float3 tangentY=_CutUp.xyz+_PoleAxis.xyz*dy;
  float3 wallNormal=normalize(cross(tangentX,tangentY));
  float3 nativeNormal=UnityObjectToWorldNormal(v.normal);
  float3 shift=wallNormal-_CutNormal.xyz;
  nativeNormal=normalize(nativeNormal+shift*sign(dot(nativeNormal,_CutNormal.xyz)));
  v.normal=normalize(mul(nativeNormal,(float3x3)unity_ObjectToWorld));
  v.vertex.xyz+=mul((float3x3)unity_WorldToObject,offset);
 }
 void surf(Input IN,inout SurfaceOutputStandardSpecular o){
  float radius=3;
  if (_CutOpen>.001 && abs(IN.cut.z)<2.35 && abs(IN.cut.x)<2.4 && abs(IN.cut.y)<.8)
   radius=length(SplitCoordinates(IN.cut));
  float local=1-smoothstep(1.25,2.35,abs(IN.cut.z));
  float inside=local*_CutOpen*(1-smoothstep(.54,1.12,radius));
  float core=local*_CutOpen*(1-smoothstep(.12,.52,radius));
  fixed4 c=tex2D(_MainTex,IN.uv_MainTex)*_Color;
  o.Albedo=c.rgb*lerp(1,.19,inside);
  o.Normal=UnpackScaleNormal(tex2D(_BumpMap,IN.uv_BumpMap),_BumpScale);
  o.Specular=_SpecColor.rgb*lerp(1,.46,inside);
  o.Smoothness=lerp(_Glossiness,.28,inside);
  o.Emission=float3(.25,.026,.012)*core*core;
  o.Alpha=1;
 }
 ENDCG
 }
 Fallback "Standard (Specular setup)"
}

