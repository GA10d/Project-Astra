Shader "Hidden/Astra/ToolV2/SignalFX"
{
    Properties { _MainTex ("Texture", 2D) = "white" {} }
    SubShader
    {
        Cull Off ZWrite Off ZTest Always
        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _MainTex;
            float _Mode, _Clock;
            fixed4 frag(v2f_img i) : SV_Target
            {
                float2 uv=i.uv;
                float scanWave=sin(uv.y*650+_Clock*23);
                if(_Mode<1.5)
                {
                    float band=step(.965,frac(uv.y*15+_Clock*.9));
                    uv.x+=band*sin(_Clock*19+uv.y*160)*.011;
                }
                else if(_Mode<2.5) uv.x+=sin(uv.y*53+_Clock*12)*.0035;
                float split=_Mode<1.5?.0025:_Mode<2.5?.001:0;
                fixed4 c=tex2D(_MainTex,uv);
                if(split>0){c.r=tex2D(_MainTex,uv+float2(split,0)).r;c.b=tex2D(_MainTex,uv-float2(split,0)).b;}
                c.rgb*=1-(scanWave*.5+.5)*.065;
                if(_Mode>2.5)c.rgb*=fixed3(1.17,.83,.78);
                return c;
            }
            ENDCG
        }
    }
}
