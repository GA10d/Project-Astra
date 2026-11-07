Shader "Astra/Zyl/FarewellEnergy"
{
    Properties
    {
        _Color("Energy", Color) = (0.02, 0.82, 1, 1)
        _PhaseTime("Animation time", Float) = 0
        _SurfaceWaveAmplitude("Radial surface wave amplitude", Range(0, .06)) = .0163
        _InnerWaveHz("Inner wave frequency", Float) = 1.3
        _HaloWaveHz("Outer wave frequency", Float) = .55
        _HaloIntensity("Outer halo intensity", Float) = 1.05
        _CoreFlash("Core flash", Range(0, 1)) = 0
    }
    SubShader
    {
        Tags { "Queue"="Geometry+12" "RenderType"="Opaque" }

        CGINCLUDE
        #include "UnityCG.cginc"
        float4 _Color;
        float _PhaseTime, _SurfaceWaveAmplitude, _InnerWaveHz, _HaloWaveHz, _HaloIntensity, _CoreFlash;

        struct Varyings
        {
            float4 position : SV_POSITION;
            float3 direction : TEXCOORD0;
            float3 normal : TEXCOORD1;
            float3 world : TEXCOORD2;
        };

        static const float3 WaveDirection = float3(6.2, 8.7, -4.1);
        static const float3 CrossWaveDirection = float3(-9.8, 3.3, 6.2);

        float2 WaveAngles(float3 direction, float phase)
        {
            return float2(dot(direction, WaveDirection) + phase,
                dot(direction, CrossWaveDirection) - phase * .71);
        }

        Varyings DisplaceSphere(appdata_base input, float scale, float phase, float amplitude)
        {
            Varyings output;
            float3 direction = normalize(input.vertex.xyz);
            float wave = dot(sin(WaveAngles(direction, phase)), float2(1, .48)) / 1.48;
            float4 vertex = float4(input.vertex.xyz * scale * (1 + amplitude * wave), 1);
            output.position = UnityObjectToClipPos(vertex);
            output.direction = direction;
            output.normal = UnityObjectToWorldNormal(input.normal);
            output.world = mul(unity_ObjectToWorld, vertex).xyz;
            return output;
        }

        float SurfacePhase()
        {
            return _PhaseTime * _InnerWaveHz * 6.283185;
        }

        Varyings vertCore(appdata_base input)
        {
            return DisplaceSphere(input, lerp(.88, 1, _CoreFlash), SurfacePhase(), _SurfaceWaveAmplitude);
        }

        Varyings vertShell(appdata_base input)
        {
            // The same radial deformation keeps the blue layer outside the core at every amplitude.
            return DisplaceSphere(input, 1.04, SurfacePhase(), _SurfaceWaveAmplitude);
        }

        Varyings vertHalo(appdata_base input)
        {
            return DisplaceSphere(input, 1.18, _PhaseTime * _HaloWaveHz * 6.283185 + 1.3, .0074);
        }

        float Facing(Varyings input)
        {
            return abs(dot(normalize(input.normal), normalize(_WorldSpaceCameraPos - input.world)));
        }
        float RippleLight(float3 direction, float phase)
        {
            float2 slope = cos(WaveAngles(direction, phase)) * float2(1, .48);
            float3 gradient = slope.x * WaveDirection + slope.y * CrossWaveDirection;
            float3 tangent = gradient - direction * dot(direction, gradient);
            return dot(tangent, float3(-.05, .07, .028));
        }
        ENDCG

        Pass
        {
            Cull Back ZTest LEqual ZWrite On
            CGPROGRAM
            #pragma vertex vertCore
            #pragma fragment frag

            float4 frag(Varyings input) : SV_Target
            {
                float facing = Facing(input);
                float phase = SurfacePhase();
                float3 direction = normalize(input.direction);
                float ripple = sin(dot(direction, float3(5, 8, -4)) - phase)
                             * sin(dot(direction, float3(-4, 2, 7)) + phase * .47);
                float field = smoothstep(-.50, .60, ripple + RippleLight(direction, phase) * .38);
                float hot = smoothstep(.40 - _CoreFlash * .35, .95 - _CoreFlash * .54,
                    facing + .17 * ripple);
                float3 blue = lerp(_Color.rgb * float3(.9, .54, .88), float3(.13, .92, 1), field);
                float3 white = lerp(float3(.84, .99, 1), float3(1, 1, 1), _CoreFlash);
                float3 color = lerp(blue, white, hot);
                color = lerp(color, float3(.50, .91, 1), pow(1 - facing, 4) * .45);
                return float4(color, 1);
            }
            ENDCG
        }

        Pass
        {
            Cull Back ZTest LEqual ZWrite Off Blend SrcAlpha OneMinusSrcAlpha
            CGPROGRAM
            #pragma vertex vertShell
            #pragma fragment frag

            float4 frag(Varyings input) : SV_Target
            {
                float facing = Facing(input);
                float3 direction = normalize(input.direction);
                float phase = -_PhaseTime * _InnerWaveHz * 6.283185 * .68;
                float ripple = saturate(.50 + RippleLight(direction, phase));
                float edge = smoothstep(0, .23, facing) * (1 - facing);
                float3 plasma = lerp(float3(.08, .48, .89), float3(.39, .93, 1), ripple);
                return float4(plasma, edge * lerp(.26, .73, ripple));
            }
            ENDCG
        }

        Pass
        {
            Cull Back ZTest LEqual ZWrite Off Blend SrcAlpha One
            CGPROGRAM
            #pragma vertex vertHalo
            #pragma fragment frag

            float4 frag(Varyings input) : SV_Target
            {
                float facing = Facing(input);
                float softRim = smoothstep(0, .30, facing) * pow(1 - facing, 3);
                float phase = _PhaseTime * _HaloWaveHz * 6.283185 + 1.3;
                float shimmer = .83 + .17 * sin(WaveAngles(normalize(input.direction), phase).x);
                return float4(.38, .80, 1, softRim * shimmer * _HaloIntensity * .72);
            }
            ENDCG
        }
    }
}
