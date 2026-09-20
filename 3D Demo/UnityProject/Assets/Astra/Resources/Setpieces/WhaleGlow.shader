Shader "Astra/Setpieces/WhaleGlow"
{
    Properties { _Color("Photophore emission",Color)=(.1,1.1,1.5,1) _Clock("Animation clock",Float)=0 }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        Cull Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            struct v2f { float4 position:SV_POSITION; float3 normal:TEXCOORD0; float3 world:TEXCOORD1; };
            float4 _Color; float _Clock;
            v2f vert(appdata_base v)
            {
                v2f o; o.position=UnityObjectToClipPos(v.vertex); o.normal=UnityObjectToWorldNormal(v.normal);
                o.world=mul(unity_ObjectToWorld,v.vertex).xyz; return o;
            }
            float4 frag(v2f i):SV_Target
            {
                float shape=.6+.4*abs(dot(normalize(i.normal),normalize(_WorldSpaceCameraPos-i.world)));
                float pulse=.86+.14*sin(_Clock*1.2+i.world.z*.28);
                return float4(_Color.rgb*shape*pulse,1);
            }
            ENDCG
        }
    }
}
