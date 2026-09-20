Shader "Astra/CRT"
{
    Properties
    {
        _Color ("Phosphor", Color) = (0.21,0.84,0.49,1)
        _Power ("Power", Range(0,1)) = 1
        _Brightness ("Phosphor brightness", Range(0,4)) = 1.25
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"
            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct v2f { float2 uv : TEXCOORD0; float4 vertex : SV_POSITION; };
            float4 _Color;
            float _Power, _Brightness;
            v2f vert(appdata v)
            {
                v2f o; o.vertex = UnityObjectToClipPos(v.vertex); o.uv = v.uv; return o;
            }
            float lineMask(float d, float width) { return 1.0-smoothstep(width,width+0.003,d); }
            float dotMark(float2 p, float2 center, float radius)
            { return 1.0-smoothstep(radius,radius+0.009,length(p-center)); }
            fixed4 frag(v2f i) : SV_Target
            {
                float2 uv = i.uv;
                float2 p = (uv-float2(0.38,0.5))*float2(1.52,1.0);
                float r = length(p);
                float scope = 1.0-smoothstep(0.363,0.373,r);
                float rings = lineMask(abs(r-0.09),0.001) + lineMask(abs(r-0.18),0.001)
                    + lineMask(abs(r-0.27),0.001) + lineMask(abs(r-0.36),0.002);
                float axes = lineMask(abs(p.x),0.001) + lineMask(abs(p.y),0.001);
                float theta = atan2(p.y,p.x);
                float angle = _Time.y * 0.65;
                float phase = frac((angle-theta)/6.2831853);
                float sweep = exp(-phase*24.0) * 0.35;
                float beam = exp(-phase*420.0) * 0.50;
                float mark = dotMark(p,float2(0.12,0.18),0.003)
                    + dotMark(p,float2(-0.19,0.065),0.003)
                    + dotMark(p,float2(0.21,-0.12),0.002);
                float radar = (rings*0.20 + axes*0.065 + sweep + beam + mark*0.85)*scope;
                // Right-side analog bars are generated live, not photographed into the screen.
                float panel = step(0.70,uv.x)*step(uv.x,0.91);
                float row = floor(uv.y*11.0);
                float barFill = 0.055 + 0.12*(0.5+0.5*sin(row*2.7 + _Time.y*0.15));
                float bars = panel*step(0.32,frac(uv.y*11.0))*step(frac(uv.y*11.0),0.42)
                    *step(uv.x,0.71+barFill)*step(0.16,uv.y)*step(uv.y,0.85);
                float scanlines = 0.88+0.12*sin(uv.y*540.0);
                float movingScan = exp(-abs(uv.y-frac(_Time.y*0.115))*95.0)*0.085;
                float2 edge = uv*(1.0-uv);
                float vignette = pow(saturate(edge.x*edge.y*18.0),0.26);
                float flicker = 0.98+0.015*sin(_Time.y*53.0)+0.005*sin(_Time.y*117.0);
                float signal = (0.013+radar+bars*0.35+movingScan)*scanlines*vignette*flicker;
                float3 offGlass = float3(0.006,0.012,0.011);
                float3 color = offGlass + _Color.rgb*signal*_Brightness*saturate(_Power);
                return fixed4(color,1);
            }
            ENDCG
        }
    }
    FallBack "Unlit/Color"
}
