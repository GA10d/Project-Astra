Shader "Astra/Outline"
{
    Properties
    {
        _OutlineColor ("Outline", Color) = (1, 0.76, 0.12, 1)
        _OutlineWidth ("World-space width", Range(0, 0.025)) = 0.0055
    }
    SubShader
    {
        Tags { "Queue"="Geometry+10" "RenderType"="Opaque" "IgnoreProjector"="True" }
        Pass
        {
            Cull Front
            ZWrite Off
            ZTest LEqual
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            float4 _OutlineColor;
            float _OutlineWidth;
            struct appdata { float4 vertex : POSITION; float3 normal : NORMAL; };
            struct v2f { float4 position : SV_POSITION; };
            v2f vert(appdata v)
            {
                v2f o;
                float3 world = mul(unity_ObjectToWorld, v.vertex).xyz;
                float3 normal = UnityObjectToWorldNormal(v.normal);
                world += normal * _OutlineWidth;
                o.position = mul(UNITY_MATRIX_VP, float4(world, 1));
                return o;
            }
            fixed4 frag(v2f i) : SV_Target { return _OutlineColor; }
            ENDCG
        }
    }
    Fallback Off
}
