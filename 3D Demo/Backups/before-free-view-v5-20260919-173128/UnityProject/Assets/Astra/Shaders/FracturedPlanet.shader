Shader "Astra/FracturedPlanet"
{
    Properties
    {
        _Color("Ocean",Color)=(0.035,0.08,0.12,1)
        _FragmentOrigin("Original object-space center",Vector)=(0,0,0,0)
        _Heat("Break-up heat",Range(0,1))=0
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
            struct v2f { float4 pos:SV_POSITION; float3 normal:TEXCOORD0; float3 world:TEXCOORD1; float3 local:TEXCOORD2; };
            float4 _Color, _FragmentOrigin;
            float _Heat;
            v2f vert(appdata_base v) { v2f o; o.pos=UnityObjectToClipPos(v.vertex); o.normal=UnityObjectToWorldNormal(v.normal); o.world=mul(unity_ObjectToWorld,v.vertex).xyz; o.local=v.vertex.xyz+_FragmentOrigin.xyz; return o; }
            float hash(float3 p) { return frac(sin(dot(p,float3(12.9898,78.233,37.719)))*43758.5453); }
            float noise(float3 p) { float3 f=frac(p); float3 a=floor(p); f=f*f*(3-2*f); return lerp(lerp(lerp(hash(a),hash(a+float3(1,0,0)),f.x),lerp(hash(a+float3(0,1,0)),hash(a+float3(1,1,0)),f.x),f.y),lerp(lerp(hash(a+float3(0,0,1)),hash(a+float3(1,0,1)),f.x),lerp(hash(a+float3(0,1,1)),hash(a+1),f.x),f.y),f.z); }
            float4 frag(v2f i):SV_Target
            {
                float3 n=normalize(i.normal); float3 v=normalize(_WorldSpaceCameraPos-i.world);
                float edge=pow(1-saturate(dot(n,v)),4);
                float clouds=smoothstep(0.49,0.71,noise(i.local*16)+noise(i.local*45)*0.18);
                float light=saturate(dot(n,normalize(float3(0.3,0.8,-0.7))));
                float3 c=lerp(_Color.rgb,float3(0.22,0.29,0.31),clouds)*(0.17+light*0.65);
                c+=float3(0.15,0.37,0.5)*edge*(0.15+light);
                c=lerp(c,c+float3(.24,.035,.003),_Heat);
                return float4(c,1);
            }
            ENDCG
        }
    }
}
