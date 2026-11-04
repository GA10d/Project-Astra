Shader "Astra/PortalHorizon" {
 Properties { _Color("Tint",Color)=(.1,.5,1,1) }
 SubShader { Tags {"Queue"="Transparent+100" "RenderType"="Transparent"} Blend SrcAlpha OneMinusSrcAlpha ZWrite Off Cull Off Pass {
 CGPROGRAM
 #pragma vertex vert
 #pragma fragment frag
 #include "UnityCG.cginc"
 struct v {float4 pos:SV_POSITION;float3 world:TEXCOORD0;};
 float _TransitBlend; float _Clock; float4 _Color; v vert(appdata_base a){v o;o.pos=UnityObjectToClipPos(a.vertex);o.world=mul(unity_ObjectToWorld,a.vertex).xyz;return o;}
 fixed4 frag(v i):SV_Target {float2 p=float2(i.world.y-1.65,i.world.z);float r=length(p);float a=atan2(p.y,p.x);float bands=pow(.5+.5*sin(r*17-a*5-_Clock*3),5);float spiral=pow(.5+.5*sin(a*9+r*8+_Clock*2),12);float core=exp(-r*2);return float4(float3(.015,.025,.09)+bands*float3(.12,.25,.6)+spiral*float3(.3,.08,.4)+core*float3(.1,.3,.5),1-_TransitBlend);}
 ENDCG
 } } }
