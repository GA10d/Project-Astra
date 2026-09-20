Shader "Astra/CabinGrade"
{
    Properties { _MainTex("Source", 2D) = "white" {} _Exposure("Exposure", Float) = 1.12 }
    SubShader
    {
        Cull Off ZWrite Off ZTest Always
        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _MainTex; float _Exposure;
            fixed4 frag(v2f_img i) : SV_Target
            {
                float3 c = tex2D(_MainTex,i.uv).rgb * _Exposure;
                c = (c * (2.51*c+0.03))/(c*(2.43*c+0.59)+0.14);
                float2 p = i.uv*2-1;
                c *= 1.0 - 0.17*dot(p,p);
                float grain = frac(sin(dot(i.uv*_ScreenParams.xy,float2(12.9898,78.233))+floor(_Time.y*24))*43758.5453)-0.5;
                c += grain*0.0008;
                return float4(saturate(c),1);
            }
            ENDCG
        }
    }
}
