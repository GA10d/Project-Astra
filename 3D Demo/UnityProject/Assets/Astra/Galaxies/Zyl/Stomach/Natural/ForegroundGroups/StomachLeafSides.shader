Shader "Astra/Zyl/Stomach Leaf Sides"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
        _MainTex ("Albedo", 2D) = "white" {}
        _BumpMap ("Normal Map", 2D) = "bump" {}
        _BumpScale ("Normal Scale", Float) = 1
        _SpecColor ("Specular", Color) = (0.055,0.055,0.055,1)
        _Glossiness ("Smoothness", Range(0,1)) = 0.22
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" }
        Cull Off

        CGPROGRAM
        #pragma target 3.0
        #pragma surface surf StandardSpecular fullforwardshadows addshadow
        #include "UnityStandardUtils.cginc"

        sampler2D _MainTex;
        sampler2D _BumpMap;
        half4 _Color;
        half _BumpScale;
        half _Glossiness;

        struct Input
        {
            float2 uv_MainTex;
            float2 uv_BumpMap;
            float4 color : COLOR;
            float facing : VFACE;
        };

        void surf(Input input, inout SurfaceOutputStandardSpecular output)
        {
            // Alpha marks the faces that had a reversed duplicate in the source mesh.
            half side = input.facing > 0 ? 1 : -1;
            clip(side > 0 ? 1 : input.color.a - 0.5);
            output.Albedo = tex2D(_MainTex, input.uv_MainTex).rgb * _Color.rgb;
            output.Normal = UnpackScaleNormal(tex2D(_BumpMap, input.uv_BumpMap), _BumpScale);
            output.Normal.z *= side;
            output.Specular = _SpecColor.rgb;
            output.Smoothness = _Glossiness;
            output.Occlusion = 1;
            output.Alpha = 1;
        }
        ENDCG
    }
}
