Shader "Astra/Zyl/FarewellRock"
{
    Properties
    {
        _MainTex("Crater surface", 2D) = "white" {}
        _Color("Stone", Color) = (0.85, 0.68, 0.57, 1)
        _RimColor("Rim light", Color) = (0.40, 0.26, 0.18, 1)
        _Ambient("Shadow fill", Range(0, 1)) = .24
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _Color, _RimColor;
            float _Ambient;

            struct Varyings
            {
                float4 position : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 normal : TEXCOORD1;
                float3 eye : TEXCOORD2;
            };

            Varyings vert(appdata_base input)
            {
                Varyings output;
                output.position = UnityObjectToClipPos(input.vertex);
                output.uv = input.texcoord.xy;
                output.normal = UnityObjectToWorldNormal(input.normal);
                output.eye = _WorldSpaceCameraPos - mul(unity_ObjectToWorld, input.vertex).xyz;
                return output;
            }

            float4 frag(Varyings input) : SV_Target
            {
                float3 normal = normalize(input.normal);
                float3 eye = normalize(input.eye);
                float key = saturate(dot(normal, normalize(float3(-.56, .72, -.40))));
                float rim = pow(1 - saturate(dot(normal, eye)), 3);
                float3 textureColor = tex2D(_MainTex, input.uv).rgb;
                float detail = dot(textureColor, float3(.30, .59, .11));
                float3 stone = _Color.rgb * lerp(.34, 1, saturate(detail * 1.36));
                return float4(stone * (_Ambient + key * .87) + _RimColor.rgb * rim * (.14 + .46 * key), 1);
            }
            ENDCG
        }
    }
}
