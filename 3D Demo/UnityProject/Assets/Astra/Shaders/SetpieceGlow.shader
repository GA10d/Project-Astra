Shader "Astra/SetpieceGlow"
{
    Properties { _Color("Color",Color)=(1,.3,.03,1) _Radial("Radial sprite",Float)=0 _DstBlend("Destination blend",Float)=1 }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        Blend SrcAlpha [_DstBlend]
        ZWrite Off
        Cull Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            struct appdata { float4 vertex:POSITION; float2 uv:TEXCOORD0; float4 color:COLOR; };
            struct v2f { float4 vertex:SV_POSITION; float2 uv:TEXCOORD0; float4 color:COLOR; };
            float4 _Color; float _Radial;
            v2f vert(appdata v) { v2f o; o.vertex=UnityObjectToClipPos(v.vertex); o.uv=v.uv; o.color=v.color; return o; }
            float4 frag(v2f i):SV_Target
            {
                float band=pow(saturate(1-abs(i.uv.y*2-1)),1.5);
                float radial=pow(saturate(1-length(i.uv*2-1)),2);
                float alpha=lerp(band,radial,saturate(_Radial));
                return float4(_Color.rgb*i.color.rgb,_Color.a*i.color.a*alpha);
            }
            ENDCG
        }
    }
}
