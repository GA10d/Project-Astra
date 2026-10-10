Shader "Astra/ToolV2/Character"
{
    Properties
    {
        _Color ("Palette tint", Color)=(.5,.5,.4,1)
        _Bands ("Lighting bands", Range(2,8))=5
        _Stylized ("Stylization", Range(0,1))=1
        _SurfaceScale ("Material grain", Float)=65
        _FaceMarkTex ("Face paint mask (UV2)", 2D)="black" {}
        _FaceMarkColor ("Face paint color", Color)=(.5,.6,.4,1)
        _FaceMarkEnabled ("Face paint enabled", Float)=0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        CGPROGRAM
        #pragma surface surf Banded fullforwardshadows addshadow
        #pragma target 3.0
        #include "UnityPBSLighting.cginc"
        half4 _Color; half _Bands, _Stylized; float _SurfaceScale;
        sampler2D _FaceMarkTex; half4 _FaceMarkColor; half _FaceMarkEnabled;
        struct Input { float3 worldPos; float2 uv2_FaceMarkTex; };
        half4 LightingBanded(SurfaceOutput s, half3 lightDir, half atten)
        {
            half ndl=saturate(dot(s.Normal,lightDir));
            half stepped=floor(ndl*(_Bands-1)+.5)/max(1,_Bands-1);
            half diffuse=lerp(ndl,stepped,_Stylized);
            return half4(s.Albedo*_LightColor0.rgb*diffuse*atten,s.Alpha);
        }
        void surf(Input IN,inout SurfaceOutput o)
        {
            float3 p=floor(IN.worldPos*_SurfaceScale);
            half grain=frac(sin(dot(p,float3(12.31,41.23,72.19)))*413.5);
            half paint=tex2D(_FaceMarkTex,IN.uv2_FaceMarkTex).r*_FaceMarkEnabled;
            o.Albedo=lerp(_Color.rgb,_FaceMarkColor.rgb,saturate(paint))*(.97+grain*.06*_Stylized);
            o.Alpha=1;
        }
        ENDCG
    }
    Fallback "Diffuse"
}
