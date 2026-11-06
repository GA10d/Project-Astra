Shader "Astra/Zyl/FoolsMucosa" {
 Properties {
 [HideInInspector] _CutOpen ("Opening",Float)=0
 [HideInInspector] _Poke ("Impact",Float)=0
 [HideInInspector] _CutCenter ("Cut center",Vector)=(0,0,0,0)
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
 float _CutOpen,_Poke;float4 _CutCenter,_CutNormal,_CutRight,_CutUp;
 float _BreathAmplitude,_BreathPeriod,_BreathPreviewTime;
 float _UseBreathFrameMatrix;float4x4 _BreathWorldToAuth,_BreathAuthToWorld;
 float4 _BreathFrameOffset;float _BreathFrameYaw;
 float3 RotateFrame(float3 p,float angle){float sn,cs;sincos(radians(angle),sn,cs);return float3(cs*p.x+sn*p.z,p.y,-sn*p.x+cs*p.z);}
 struct Input {float2 uv_MainTex;float2 uv_BumpMap;float3 worldPos;};
 void vert(inout appdata_full v) {
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
  float plane=length(float2(dot(delta,_CutRight.xyz)/2.8,dot(delta,_CutUp.xyz)/2.2));
  float local=saturate(1-plane)*saturate(1-abs(dot(delta,_CutNormal.xyz))/3);
  // The contacted membrane is held by the pole while the rest of the stomach keeps breathing.
  offset=offset*(1-local*local)+_CutNormal.xyz*(_Poke*.7*local*local);
  v.vertex.xyz+=mul((float3x3)unity_WorldToObject,offset);
 }
 void surf(Input IN,inout SurfaceOutputStandardSpecular o){
  float3 delta=IN.worldPos-_CutCenter.xyz;
  float2 q=float2(dot(delta,_CutRight.xyz),dot(delta,_CutUp.xyz));
  float angle=atan2(q.y,q.x);
  float ragged=1+.055*sin(angle*7)+.03*sin(angle*13+1);
  float width=max(.001,1.42*sqrt(_CutOpen));
  float height=max(.001,(.06+.45*_CutOpen)*_CutOpen);
  float radius=length(q/float2(width,height));
  if(_CutOpen>.001 && abs(dot(delta,_CutNormal.xyz))<2.5)clip(radius-ragged);
  fixed4 c=tex2D(_MainTex,IN.uv_MainTex)*_Color;o.Albedo=c.rgb;
  o.Normal=UnpackScaleNormal(tex2D(_BumpMap,IN.uv_BumpMap),_BumpScale);
  o.Specular=_SpecColor.rgb;o.Smoothness=_Glossiness;o.Alpha=1;
 }
 ENDCG
 }
 Fallback "Standard (Specular setup)"
}

