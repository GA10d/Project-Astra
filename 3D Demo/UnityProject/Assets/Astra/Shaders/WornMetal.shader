Shader "Astra/WornMetal"
{
    Properties
    {
        _Color ("Old enamel / fallback tint", Color) = (0.19, 0.26, 0.23, 1)
        _RustColor ("Exposed oxidation", Color) = (0.19, 0.075, 0.027, 1)
        _MainTex ("Naval enamel color", 2D) = "white" {}
        _TextureStrength ("Color texture contribution", Range(0,1)) = 0
        _TextureScale ("Color repeats per metre", Float) = 1.0
        [Normal] _BumpMap ("Metal micro-surface normal", 2D) = "bump" {}
        _BumpScale ("Micro-surface depth", Range(0,1)) = 0.24
        _RoughnessMap ("Metal roughness (linear)", 2D) = "gray" {}
        _DetailStrength ("PBR detail contribution", Range(0,1)) = 0
        _DetailScale ("Micro-surface repeats per metre", Float) = 3.4
        _Metallic ("Metal beneath enamel", Range(0,1)) = 0.32
        _Glossiness ("Enamel smoothness", Range(0,1)) = 0.30
        _Wear ("Additional fine paint loss", Range(0,1)) = 0.16
        _Scale ("Patina density", Float) = 2.5
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 300
        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows addshadow
        #pragma target 3.0
        #include "UnityCG.cginc"
        sampler2D _MainTex, _BumpMap, _RoughnessMap;
        fixed4 _Color, _RustColor;
        half _Metallic, _Glossiness, _Wear, _TextureStrength, _DetailStrength, _BumpScale;
        float _Scale, _TextureScale, _DetailScale;
        struct Input { float3 worldPos; float3 worldNormal; INTERNAL_DATA };

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
        float3 sampleColor(float3 p, float3 weights)
        {
            return tex2D(_MainTex, p.zy).rgb * weights.x
                 + tex2D(_MainTex, p.xz).rgb * weights.y
                 + tex2D(_MainTex, p.xy).rgb * weights.z;
        }
        float3 safeUnit(float3 value, float3 fallback)
        {
            // Degenerate UV tangents (for example at knob caps) must not feed
            // normalize(0) into lighting. HDR NaNs spread across the desktop blur.
            float lengthSquared = dot(value, value);
            return lengthSquared > 0.00000001
                ? value * rsqrt(max(lengthSquared, 0.00000001)) : fallback;
        }
        void surf(Input IN, inout SurfaceOutputStandard o)
        {
            // World projection gives small plates, pipes and bevels a consistent
            // physical scale without relying on the FBX's layout / overlapping UVs.
            float3 n = safeUnit(WorldNormalVector(IN, float3(0,0,1)), float3(0,1,0));
            float3 weights = pow(abs(n), 5.0);
            weights /= max(dot(weights, float3(1,1,1)), 0.0001);
            float3 p = IN.worldPos * max(0.01, _Scale);
            float fine = noise3(p * 17.1 + 2.7);
            float grain = noise3(p * 109.0);
            float stain = noise3(p * 2.4 + 17.3);
            float3 imageColor = sampleColor(IN.worldPos * max(0.01, _TextureScale), weights);
            float3 enamel = lerp(_Color.rgb, imageColor, _TextureStrength);
            enamel *= 0.955 + stain * 0.065 + grain * 0.018;

            // Authored color is primary; extra procedural wear is sparse and tiny.
            // In particular, no low-frequency noise creates metre-wide rust blobs.
            float chipMask = smoothstep(0.79, 0.91, fine) * _Wear;
            float3 rust = _RustColor.rgb * (0.8 + grain * 0.30);
            o.Albedo = lerp(enamel, rust, chipMask * 0.65);

            float3 detailP = IN.worldPos * max(0.01, _DetailScale);
            half roughness = tex2D(_RoughnessMap, detailP.zy).r * weights.x
                           + tex2D(_RoughnessMap, detailP.xz).r * weights.y
                           + tex2D(_RoughnessMap, detailP.xy).r * weights.z;
            // Material smoothness still distinguishes brass, paint, canvas and rubber.
            half mappedSmoothness = saturate(_Glossiness + (0.55 - roughness) * 0.44);
            o.Smoothness = saturate(lerp(_Glossiness, mappedSmoothness, _DetailStrength) - chipMask * 0.18);
            o.Metallic = saturate(_Metallic * (1.0 - chipMask * 0.55));
            o.Occlusion = 1.0 - chipMask * 0.06;

            // Project normal gradients onto the geometric tangent plane. A neutral
            // normal stays neutral even on negative-facing walls or curved pipes.
            float3 nx = UnpackNormal(tex2D(_BumpMap, detailP.zy));
            float3 ny = UnpackNormal(tex2D(_BumpMap, detailP.xz));
            float3 nz = UnpackNormal(tex2D(_BumpMap, detailP.xy));
            float3 gradient = float3(0, nx.y, nx.x) * weights.x
                            + float3(ny.x, 0, ny.y) * weights.y
                            + float3(nz.x, nz.y, 0) * weights.z;
            gradient -= n * dot(n, gradient);
            float3 detailedNormal = safeUnit(n + gradient * (_BumpScale * _DetailStrength), n);
            float3 worldTangent = safeUnit(WorldNormalVector(IN, float3(1,0,0)), float3(0,0,0));
            float3 worldBitangent = safeUnit(WorldNormalVector(IN, float3(0,1,0)), float3(0,0,0));
            o.Normal = safeUnit(float3(dot(detailedNormal, worldTangent),
                                      dot(detailedNormal, worldBitangent), dot(detailedNormal, n)), float3(0,0,1));
            o.Alpha = 1;
        }
        ENDCG
    }
    FallBack "Standard"
}
