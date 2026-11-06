Shader "Astra/FabricatorLayer" {
 Properties { _Progress("Progress",Range(0,1))=0 _Color("Color",Color)=(0.43,0.56,0.52,1) }
 SubShader { Tags { "RenderType"="Opaque" } Cull Off
 CGPROGRAM
 #pragma surface surf Standard fullforwardshadows addshadow
 #pragma target 3.0
 float _Progress; fixed4 _Color;
 struct Input { float3 worldPos; };
 void surf(Input IN,inout SurfaceOutputStandard o) {
 float y=mul(unity_WorldToObject,float4(IN.worldPos,1)).y;
 clip(_Progress*0.121-y-0.0001);
 float band=1-saturate(abs(y-_Progress*0.12)/0.006);
 float layers=0.8+0.2*sin(y*2200);
 o.Albedo=_Color.rgb*layers;o.Metallic=0.65;o.Smoothness=0.35;
 o.Emission=float3(0.2,0.9,0.65)*band*2;
 }
 ENDCG
 } FallBack "Diffuse"
}
