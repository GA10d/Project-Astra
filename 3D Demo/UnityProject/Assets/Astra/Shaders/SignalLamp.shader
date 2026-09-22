Shader "Astra/SignalLamp"
{
    Properties
    {
        _Color ("Lens body", Color) = (0.15,0.22,0.12,1)
        [HDR] _EmissionColor ("Powered signal", Color) = (0.6,0.8,0.4,1)
        _Glossiness ("Lens smoothness", Range(0,1)) = 0.18
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows addshadow
        #pragma target 3.0
        fixed4 _Color;
        half4 _EmissionColor;
        half _Glossiness;
        struct Input { float3 worldPos; };
        void surf(Input IN, inout SurfaceOutputStandard o)
        {
            // Emission is always compiled. CabinSystems drives the same property
            // block to black on power-off; no Standard keyword can strip that path.
            o.Albedo = _Color.rgb;
            o.Emission = _EmissionColor.rgb;
            o.Metallic = 0;
            o.Smoothness = _Glossiness;
            o.Alpha = 1;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
