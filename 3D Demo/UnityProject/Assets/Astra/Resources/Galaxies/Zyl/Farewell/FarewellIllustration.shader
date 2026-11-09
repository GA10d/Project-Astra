Shader "Astra/Zyl/FarewellIllustration"
{
    Properties
    {
        _Frame ("Illustration frame", 2D) = "black" {}
    }
    SubShader
    {
        Tags { "Queue"="Geometry" "RenderType"="Opaque" "IgnoreProjector"="True" }
        Cull Off
        ZWrite On
        ZTest LEqual

        Pass
        {
            CGPROGRAM
            #pragma target 3.0
            #pragma vertex vert_img
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _Frame;
            float4 _FrameRocks[6], _RockTargets[6];

            float4 _OpeningFit, _DebrisFit;
            float _OpeningFrame;

            float2 FitUV(float2 uv, float4 placement)
            {
                float2 edge = min(uv, 1 - uv);
                float interior = smoothstep(0, .065, min(edge.x, edge.y));
                return lerp(uv, (uv - placement.xy) / placement.zw, interior);
            }

            void AccumulateFit(float2 uv, float4 source, float4 target, float4 placement, inout float3 fit)
            {
                if (source.z <= 0 || target.z <= 0) return;
                // Cap the warp when a custom blast timing leaves several live rocks crowded together.
                float2 baseCenter = source.xy * placement.zw + placement.xy;
                float2 center = baseCenter + clamp(target.xy - baseCenter, -.10, .10);
                float2 halfSize = clamp(target.zw, source.zw * .65, source.zw * 1.55);
                float2 relative = (uv - center) / halfSize;
                float distance = max(abs(relative.x), abs(relative.y));
                float weight = 1 - smoothstep(1, 2.15, distance);
                // Keep the scale adjustment local to the silhouette so stray debris doesn't stretch at its edge.
                float2 scaleOffset = clamp(relative, -1, 1) * (source.zw - halfSize / placement.zw);
                fit += float3((source.xy - FitUV(center, placement) + scaleOffset) * weight, weight);
            }

            fixed4 frag(v2f_img input) : SV_Target
            {
                float3 fit = 0;
                float4 placement = lerp(_DebrisFit, _OpeningFit, _OpeningFrame);
                [unroll] for (int i = 0; i < 6; i++)
                    AccumulateFit(input.uv, _FrameRocks[i], _RockTargets[i], placement, fit);

                float2 uv = FitUV(input.uv, placement) + fit.xy / max(1, fit.z);
                return fixed4(tex2D(_Frame, uv).rgb, 1);
            }
            ENDCG
        }
    }
}
