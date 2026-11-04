Shader "Astra/PortalPlanet" {
 Properties { _Color("Surface tint",Color)=(1,1,1,1) _MainTex("Crater albedo",2D)="white" {} }
 SubShader { Tags {"RenderType"="Opaque"} Pass {
 CGPROGRAM
 #pragma vertex vert
 #pragma fragment frag
 #include "UnityCG.cginc"
 struct v {float4 pos:SV_POSITION;float3 normal:TEXCOORD0;float2 uv:TEXCOORD1;};float4 _Color; sampler2D _MainTex; float4 _MainTex_ST;
 v vert(appdata_base a){v o;o.pos=UnityObjectToClipPos(a.vertex);o.normal=UnityObjectToWorldNormal(a.normal);o.uv=TRANSFORM_TEX(a.texcoord,_MainTex);return o;}
 fixed4 frag(v i):SV_Target {float3 n=normalize(i.normal);float light=.12+.88*max(0,dot(n,normalize(float3(-.6,.7,-.4))));float3 rock=tex2D(_MainTex,i.uv).rgb;rock=saturate((rock-.35)*1.8+.35);return float4(rock*_Color.rgb*light*1.65,1);}
 ENDCG
 } } }
