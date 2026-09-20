Shader "Astra/Setpieces/CrimsonLava"
{
    Properties
    {
        _Color("Cold crust", Color)=(0.032,0.014,0.02,1)
        _Glow("Molten fissures", Color)=(1.4,0.15,0.012,1)
        _Seed("Geology seed", Float)=1
        _Reveal("Arrival", Range(0,1))=1
        _Clock("Local animation clock", Float)=0
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
            struct v2f { float4 pos:SV_POSITION; float3 p:TEXCOORD0; float3 normal:TEXCOORD1; float3 world:TEXCOORD2; };
            float4 _Color, _Glow; float _Seed, _Reveal, _Clock;
            v2f vert(appdata_base v) { v2f o; o.pos=UnityObjectToClipPos(v.vertex); o.p=v.vertex.xyz*2; o.normal=UnityObjectToWorldNormal(v.normal); o.world=mul(unity_ObjectToWorld,v.vertex).xyz; return o; }
            float hash(float3 p) { return frac(sin(dot(p,float3(17.73,43.12,97.91)))*43758.54); }
            float noise(float3 p)
            {
                float3 a=floor(p), f=frac(p); f=f*f*(3-2*f);
                return lerp(lerp(lerp(hash(a),hash(a+float3(1,0,0)),f.x),lerp(hash(a+float3(0,1,0)),hash(a+float3(1,1,0)),f.x),f.y),lerp(lerp(hash(a+float3(0,0,1)),hash(a+float3(1,0,1)),f.x),lerp(hash(a+float3(0,1,1)),hash(a+1),f.x),f.y),f.z);
            }
            float fbm(float3 p) { return noise(p)*.58+noise(p*2.09)*.28+noise(p*4.31)*.14; }
            float4 frag(v2f i):SV_Target
            {
                float3 p=i.p*5.4+_Seed*float3(.9,2.3,1.7);
                float warp=fbm(p*.73);
                float geology=fbm(p+warp*2.6);
                float narrow=1-smoothstep(.015,.07,abs(geology-.5));
                float branch=(1-smoothstep(.015,.033,abs(noise(p*3.6)-.5)))*smoothstep(.36,.66,geology)*.5;
                float molten=saturate(narrow+branch);
                float pulse=.68+.28*sin(_Clock*.27+warp*15+_Seed);
                float lit=.25+.6*saturate(dot(normalize(i.normal),normalize(float3(-.7,.6,-.2))));
                float3 c=_Color.rgb*(.55+noise(p*18)*.8)*lit;
                c+=_Glow.rgb*pow(molten,2)*pulse;
                float rim=pow(1-saturate(dot(normalize(i.normal),normalize(_WorldSpaceCameraPos-i.world))),3);
                c+=float3(.23,.012,.004)*rim;
                return float4(c*_Reveal,1);
            }
            ENDCG
        }
    }
}
