Shader "Astra/Zyl/SproutWave"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
        _MainTex ("Flesh", 2D) = "white" {}
        _SpecColor ("Specular", Color) = (.17,.12,.105,1)
        _Glossiness ("Smoothness", Range(0,1)) = .62
        _WaveCenter ("Object-space wave center", Vector) = (0,0,0,0)
        _WaveAmplitude ("Radial amplitude", Range(0,.15)) = .02
        _WavePhase ("Wave phase", Float) = 0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        CGPROGRAM
        #pragma target 3.0
        #pragma surface surf StandardSpecular vertex:displace addshadow fullforwardshadows

        sampler2D _MainTex;
        fixed4 _Color;
        half _Glossiness;
        float3 _WaveCenter;
        float _WaveAmplitude, _WavePhase;
        struct Input { float2 uv_MainTex; };

        void displace(inout appdata_full v)
        {
            const float3 first = float3(6.2, 8.7, -4.1);
            const float3 second = float3(-9.8, 3.3, 6.2);
            float3 radius = v.vertex.xyz - _WaveCenter;
            float3 direction = normalize(radius);
            float2 angle = float2(dot(direction, first) + _WavePhase,
                dot(direction, second) - .71 * _WavePhase);
            float wave = dot(sin(angle), float2(1, .48)) / 1.48;
            // The underside stays still where the roots join the crown.
            float pinned = saturate((direction.y + .9) / .45);
            float weight = pinned * pinned * (3 - 2 * pinned);
            float3 slope = (cos(angle.x) * first + .48 * cos(angle.y) * second) / 1.48;
            slope = slope * weight + float3(0, wave * 6 * pinned * (1 - pinned) / .45, 0);
            slope -= direction * dot(slope, direction);
            v.vertex.xyz += radius * (_WaveAmplitude * wave * weight);
            v.normal = normalize(v.normal - _WaveAmplitude * slope);
        }

        void surf(Input input, inout SurfaceOutputStandardSpecular surface)
        {
            fixed4 color = tex2D(_MainTex, input.uv_MainTex) * _Color;
            surface.Albedo = color.rgb;
            surface.Specular = _SpecColor.rgb;
            surface.Smoothness = _Glossiness;
            surface.Alpha = color.a;
        }
        ENDCG
    }
    FallBack "Standard (Specular setup)"
}
