Shader "Astra/Setpieces/MirrorHull"
{
    Properties
    {
        _Color ("Aged enamel",Color) = (.24,.29,.21,1)
        _Metallic ("Metal",Range(0,1)) = .4
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 200
        Cull Off
        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows
        #pragma target 3.0
        fixed4 _Color; half _Metallic;
        struct Input { float3 worldPos; };
        float hash(float3 p) { p=frac(p*.3183099+.17);p*=17;return frac(p.x*p.y*p.z*(p.x+p.y+p.z)); }
        float noise(float3 p)
        {
            float3 i=floor(p), f=frac(p);f=f*f*(3-2*f);
            return lerp(lerp(lerp(hash(i),hash(i+float3(1,0,0)),f.x),lerp(hash(i+float3(0,1,0)),hash(i+float3(1,1,0)),f.x),f.y),
                lerp(lerp(hash(i+float3(0,0,1)),hash(i+float3(1,0,1)),f.x),lerp(hash(i+float3(0,1,1)),hash(i+1),f.x),f.y),f.z);
        }
        void surf(Input IN,inout SurfaceOutputStandard o)
        {
            float3 p=mul(unity_WorldToObject,float4(IN.worldPos,1)).xyz;
            float grains=noise(p*32), scratches=noise(p*float3(170,6,75));
            float chip=smoothstep(.60,.74,noise(p*8)+grains*.17);
            float3 rust=float3(.17,.061,.024)*(1+grains*.50);
            o.Albedo=lerp(_Color.rgb*(.67+.40*grains),rust,chip)*(.87+.13*scratches);
            o.Metallic=lerp(_Metallic,.16,chip);o.Smoothness=lerp(.33,.10,chip);
            o.Emission=o.Albedo*.026;o.Occlusion=.93;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
