Shader "Astra/Setpieces/SortingScan"
{
    Properties { _Pulse ("Inspection beam strength", Range(0,1)) = 0.65 }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        Blend SrcAlpha One
        ZWrite Off
        ZTest LEqual
        Cull Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            float _Pulse;
            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct v2f { float4 position : SV_POSITION; float2 uv : TEXCOORD0; };
            v2f vert(appdata v) { v2f o; o.position = UnityObjectToClipPos(v.vertex); o.uv = v.uv; return o; }
            fixed4 frag(v2f i) : SV_Target
            {
                float edge = pow(saturate(1 - abs(i.uv.y - .5) * 2), .7);
                return fixed4(.07, .76, .85, (.075 + edge * .075) * _Pulse);
            }
            ENDCG
        }
    }
}
