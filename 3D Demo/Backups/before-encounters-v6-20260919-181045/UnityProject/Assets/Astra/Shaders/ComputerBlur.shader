Shader "Astra/ComputerBlur"
{
    Properties { _MainTex ("Source", 2D) = "white" {} }
    SubShader
    {
        Cull Off ZWrite Off ZTest Always
        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _MainTex; float4 _Direction;
            fixed4 frag(v2f_img i) : SV_Target
            {
                fixed4 c = tex2D(_MainTex, i.uv) * 0.227027;
                c += tex2D(_MainTex, i.uv + _Direction.xy * 1.384615) * 0.316216;
                c += tex2D(_MainTex, i.uv - _Direction.xy * 1.384615) * 0.316216;
                c += tex2D(_MainTex, i.uv + _Direction.xy * 3.230769) * 0.070270;
                c += tex2D(_MainTex, i.uv - _Direction.xy * 3.230769) * 0.070270;
                return c;
            }
            ENDCG
        }
    }
}
