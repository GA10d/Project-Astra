Shader "Astra/Zyl/DigestiveWater"
{
    Properties
    {
        [NoScaleOffset] _WaveMap ("Water wave texture", 2D) = "gray" {}
        [NoScaleOffset] _CavityReflection ("Baked stomach reflection", Cube) = "" {}
        _BodyColor ("Digestive fluid tint", Color) = (.24, .080, .034, 1)
        _ReflectionTint ("Reflection tint", Color) = (1, .81, .70, 1)
        _WaveStrength ("Ripple strength", Range(0, 1)) = .32
        _WaveTiling ("Ripple scale", Range(.02, .6)) = .16
        _FlowSpeed ("Flow speed", Range(0, 3)) = 1
        _ReflectionStrength ("Reflection strength", Range(0, 1.5)) = .9
        [HideInInspector] _WaterPreviewTime ("Preview time (-1 = realtime)", Float) = -1
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" "DisableBatching" = "True" }
        Pass
        {
            Name "WATER"
            Tags { "LightMode" = "ForwardBase" }
            ZWrite On

            CGPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 3.0
            #pragma multi_compile_fog
            #include "UnityCG.cginc"

            sampler2D _WaveMap;
            samplerCUBE _CavityReflection;
            float4 _BodyColor, _ReflectionTint;
            float _WaveStrength, _WaveTiling, _FlowSpeed, _ReflectionStrength, _WaterPreviewTime;

            struct Varyings
            {
                float4 position : SV_POSITION;
                float2 surface : TEXCOORD0;
                float3 eye : TEXCOORD1;
                UNITY_FOG_COORDS(2)
            };

            Varyings Vert(appdata_base input)
            {
                Varyings output;
                output.position = UnityObjectToClipPos(input.vertex);
                // The imported basin lies in local XY, with local +Z facing up.
                // Use its axes so the baked reflection follows the cavity when the scene rotates.
                float3 right = normalize(unity_ObjectToWorld._m00_m10_m20);
                float3 up = normalize(unity_ObjectToWorld._m02_m12_m22);
                float3 forward = -normalize(unity_ObjectToWorld._m01_m11_m21);
                float3 eye = _WorldSpaceCameraPos - mul(unity_ObjectToWorld, input.vertex).xyz;
                output.surface = float2(input.vertex.x, -input.vertex.y);
                output.eye = float3(dot(eye, right), dot(eye, up), dot(eye, forward));
                UNITY_TRANSFER_FOG(output, output.position);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float time = (_WaterPreviewTime < 0 ? _Time.y : _WaterPreviewTime) * _FlowSpeed;
                float2 uv = input.surface * _WaveTiling;
                float2 across = float2(.8 * uv.x - .6 * uv.y, .6 * uv.x + .8 * uv.y);
                half4 broad = tex2D(_WaveMap, uv * float2(.72, 1.15) + time * float2(.022, -.016));
                half4 fine = tex2D(_WaveMap, across * 2.15 + time * float2(-.037, .028));
                half2 fineSlope = fine.rg * 2 - 1;
                fineSlope = half2(.8 * fineSlope.x + .6 * fineSlope.y, -.6 * fineSlope.x + .8 * fineSlope.y);
                half2 slope = ((broad.rg * 2 - 1) * .76 + fineSlope * .24) * _WaveStrength;
                half3 normal = normalize(half3(-slope.x, 1, -slope.y));
                half3 view = normalize(input.eye);
                half fresnel = .055 + .945 * pow(1 - saturate(dot(normal, view)), 4);
                half3 ray = reflect(-view, normal);
                ray.y = abs(ray.y);
                half3 reflected = texCUBE(_CavityReflection, ray).rgb * _ReflectionTint.rgb;
                half variation = broad.b * .70 + fine.b * .30;
                half3 body = _BodyColor.rgb * lerp(.66, 1.26, variation);
                half reflectWeight = saturate((.13 + fresnel * .72) * _ReflectionStrength);
                half3 color = lerp(body, reflected, reflectWeight);
                // Keep the cavity's brightest highlights visible between the amber waves.
                color += reflected * reflected * (.22 + fresnel * .24) * _ReflectionStrength;
                half4 result = half4(color, 1);
                UNITY_APPLY_FOG(input.fogCoord, result);
                return result;
            }
            ENDCG
        }
    }
    Fallback Off
}
