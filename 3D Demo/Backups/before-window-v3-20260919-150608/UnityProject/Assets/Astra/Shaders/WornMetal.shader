Shader "Astra/WornMetal"
{
    Properties
    {
        _Color ("Old enamel", Color) = (0.19, 0.26, 0.23, 1)
        _RustColor ("Exposed oxidation", Color) = (0.19, 0.075, 0.027, 1)
        _Metallic ("Metal beneath enamel", Range(0,1)) = 0.32
        _Glossiness ("Enamel smoothness", Range(0,1)) = 0.30
        _Wear ("Age / paint loss", Range(0,1)) = 0.33
        _Scale ("Texture density", Float) = 2.5
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 250
        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows addshadow
        #pragma target 3.0
        fixed4 _Color, _RustColor;
        half _Metallic, _Glossiness, _Wear;
        float _Scale;
        struct Input { float3 worldPos; float3 worldNormal; };

        float hash31(float3 p)
        {
            p = frac(p * 0.1031);
            p += dot(p, p.yzx + 33.33);
            return frac((p.x + p.y) * p.z);
        }
        float noise3(float3 p)
        {
            float3 i = floor(p), f = frac(p);
            f = f * f * (3.0 - 2.0 * f);
            float n00 = lerp(hash31(i), hash31(i + float3(1,0,0)), f.x);
            float n10 = lerp(hash31(i + float3(0,1,0)), hash31(i + float3(1,1,0)), f.x);
            float n01 = lerp(hash31(i + float3(0,0,1)), hash31(i + float3(1,0,1)), f.x);
            float n11 = lerp(hash31(i + float3(0,1,1)), hash31(i + float3(1,1,1)), f.x);
            return lerp(lerp(n00, n10, f.y), lerp(n01, n11, f.y), f.z);
        }
        void surf(Input IN, inout SurfaceOutputStandard o)
        {
            // World-space patina avoids a UV seam on each small wall/pipe module.
            float3 p = IN.worldPos * max(0.01, _Scale);
            float broad = noise3(p * 0.47 + 17.3);
            float medium = noise3(p * 3.1 + 2.7);
            float fine = noise3(p * 25.0);
            float grain = noise3(p * 108.0);
            // Most of the enamel remains calm: oxidation is confined to a few patches.
            float threshold = lerp(0.95, 0.57, _Wear);
            float corrosion = smoothstep(threshold, threshold + 0.09,
                broad * 0.76 + medium * 0.24);
            float chip = smoothstep(0.76, 0.9, fine) * corrosion;
            float3 enamel = _Color.rgb * (0.92 + medium * 0.12 + grain * 0.035);
            float3 rust = _RustColor.rgb * (0.7 + fine * 0.60);
            float3 metal = float3(0.31,0.32,0.29);
            float3 patina = lerp(rust, metal, chip * 0.35);
            // Fine, infrequent machining scratches, never a high-contrast noise blanket.
            float scratch = pow(saturate(noise3(float3(p.x*140.0,p.y*2.0,p.z*140.0))), 15.0);
            o.Albedo = lerp(enamel, patina, corrosion) + scratch * _Wear * 0.04;
            o.Metallic = lerp(_Metallic, 0.09, corrosion) + chip * 0.20;
            o.Smoothness = saturate(_Glossiness * (0.88 + fine * 0.20) - corrosion * 0.17);
            o.Occlusion = 1.0 - corrosion * 0.09;
            o.Alpha = 1;
        }
        ENDCG
    }
    FallBack "Standard"
}
