Shader "Astra/Zyl/FarewellIllustration"
{
    Properties
    {
        _First ("First frame", 2D) = "black" {}
        _Next ("Next frame", 2D) = "black" {}
        _Mix ("Frame blend", Range(0, 1)) = 0
        _Opacity ("Handoff opacity", Range(0, 1)) = 1
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Cull Off
        ZWrite Off
        ZTest LEqual
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma target 3.0
            #pragma vertex vert_img
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _First, _Next;
            half _Mix, _Opacity;

            float4 _FirstRocks[6], _NextRocks[6], _RockTargets[6];

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
                float3 firstFit = 0, nextFit = 0;
                float4 firstPlacement = lerp(_DebrisFit, _OpeningFit, _OpeningFrame);
                [unroll] for (int i = 0; i < 6; i++)
                {
                    AccumulateFit(input.uv, _FirstRocks[i], _RockTargets[i], firstPlacement, firstFit);
                    AccumulateFit(input.uv, _NextRocks[i], _RockTargets[i], _DebrisFit, nextFit);
                }
                float2 firstUV = FitUV(input.uv, firstPlacement) + firstFit.xy / max(1, firstFit.z);
                float2 nextUV = FitUV(input.uv, _DebrisFit) + nextFit.xy / max(1, nextFit.z);
                fixed3 color = lerp(tex2D(_First, firstUV).rgb, tex2D(_Next, nextUV).rgb, _Mix);
                return fixed4(color, _Opacity);
            }
            ENDCG
        }
    }
}
