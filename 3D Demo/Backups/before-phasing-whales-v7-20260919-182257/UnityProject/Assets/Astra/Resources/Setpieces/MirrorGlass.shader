Shader "Astra/Setpieces/MirrorGlass"
{
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Back
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            struct appdata {float4 vertex:POSITION;float3 normal:NORMAL;float2 uv:TEXCOORD0;};
            struct v2f {float4 pos:SV_POSITION;float3 normal:TEXCOORD0;float3 view:TEXCOORD1;float2 uv:TEXCOORD2;};
            v2f vert(appdata v)
            {
                v2f o;o.pos=UnityObjectToClipPos(v.vertex);o.normal=UnityObjectToWorldNormal(v.normal);
                o.view=_WorldSpaceCameraPos-mul(unity_ObjectToWorld,v.vertex).xyz;o.uv=v.uv;return o;
            }
            fixed4 frag(v2f i):SV_Target
            {
                float edge=pow(1-saturate(abs(dot(normalize(i.normal),normalize(i.view)))),3);
                float scratch=smoothstep(.997,.999,sin(i.uv.y*340+i.uv.x*4))*step(.85,i.uv.x);
                return fixed4(.15,.32,.30,.024+edge*.18+scratch*.035);
            }
            ENDCG
        }
    }
}
