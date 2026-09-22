Shader "Astra/CabinGrade"
{
    Properties
    {
        _MainTex ("Source", 2D) = "white" {}
        _Exposure ("Exposure", Range(0.5,2)) = 1.12
        _StyleStrength ("Limited tones", Range(0,1)) = 1
        _ColorSteps ("Display-space tone levels", Range(4,64)) = 24
        _DitherStrength ("Ordered dither", Range(0,1)) = 0.65
        _DitherSize ("Dither cell pixels", Range(1,4)) = 1
        _Vignette ("Edge darkening", Range(0,0.3)) = 0.12
    }
    SubShader
    {
        Cull Off ZWrite Off ZTest Always
        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"
            sampler2D _MainTex;
            float _Exposure, _StyleStrength, _ColorSteps, _DitherStrength, _DitherSize, _Vignette;
            float4 _OutputSize;
            float bayer2(float2 p)
            {
                return 2.0 * p.x + 3.0 * p.y - 4.0 * p.x * p.y;
            }
            float orderedThreshold(float2 pixel)
            {
                // A stationary 4x4 Bayer pattern, with no time-dependent grain.
                float2 p = fmod(floor(pixel / max(1.0, _DitherSize)), 4.0);
                return (4.0 * bayer2(fmod(p, 2.0)) + bayer2(floor(p / 2.0)) + 0.5) / 16.0 - 0.5;
            }
            float4 frag(v2f_img i) : SV_Target
            {
                float3 c = max(0, tex2D(_MainTex,i.uv).rgb * _Exposure);
                c = saturate((c * (2.51*c+0.03))/(c*(2.43*c+0.59)+0.14));
                float2 p = i.uv*2-1;
                c *= 1.0 - _Vignette*dot(p,p);
                // Quantize display values, returning linear light for the final sRGB
                // conversion. The desktop and HUD are drawn afterwards at full size.
                #if !defined(UNITY_COLORSPACE_GAMMA)
                    float3 displayColor = LinearToGammaSpace(c);
                #else
                    float3 displayColor = c;
                #endif
                float steps = max(3.0, floor(_ColorSteps) - 1.0);
                float threshold = orderedThreshold(i.uv * _OutputSize.xy) * _DitherStrength;
                float3 limited = floor(displayColor * steps + 0.5 + threshold) / steps;
                displayColor = lerp(displayColor, saturate(limited), _StyleStrength);
                #if !defined(UNITY_COLORSPACE_GAMMA)
                    c = GammaToLinearSpace(displayColor);
                #else
                    c = displayColor;
                #endif
                return float4(c,1);
            }
            ENDCG
        }
    }
}
