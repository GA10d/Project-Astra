Shader "Astra/Setpieces/WhaleSkin"
{
    Properties
    {
        _Color("Slate blue hide", Color)=(.09,.22,.29,1)
        _Clock("Animation clock", Float)=0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        Cull Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            struct v2f { float4 position:SV_POSITION; float3 local:TEXCOORD0; float3 normal:TEXCOORD1; float3 world:TEXCOORD2; };
            float4 _Color; float _Clock;
            v2f vert(appdata_base v)
            {
                v2f o; o.position=UnityObjectToClipPos(v.vertex); o.local=v.vertex.xyz;
                o.normal=UnityObjectToWorldNormal(v.normal); o.world=mul(unity_ObjectToWorld,v.vertex).xyz; return o;
            }
            float hash(float3 p) { return frac(sin(dot(p,float3(19.13,75.81,43.17)))*41317.271); }
            float noise(float3 p)
            {
                float3 a=floor(p), f=frac(p); f=f*f*(3-2*f);
                return lerp(lerp(lerp(hash(a),hash(a+float3(1,0,0)),f.x),lerp(hash(a+float3(0,1,0)),hash(a+float3(1,1,0)),f.x),f.y),lerp(lerp(hash(a+float3(0,0,1)),hash(a+float3(1,0,1)),f.x),lerp(hash(a+float3(0,1,1)),hash(a+1),f.x),f.y),f.z);
            }
            float4 frag(v2f i):SV_Target
            {
                float3 n=normalize(i.normal), view=normalize(_WorldSpaceCameraPos-i.world);
                float light=.34+.7*abs(dot(n,normalize(float3(-.4,.7,.2))));
                float belly=1-smoothstep(-.1,.38,i.local.y);
                float mottle=noise(i.local*6)*.65+noise(i.local*19)*.35;
                float3 color=lerp(_Color.rgb,float3(.19,.36,.4),belly*.42)*(.62+.45*mottle)*light;
                float pleats=pow(saturate(.5+.5*sin(i.local.z*13+i.local.x*1.3)),20);
                float side=smoothstep(.2,.7,abs(i.local.x));
                float pulse=.65+.35*sin(_Clock*.7-i.local.z*.7);
                color+=float3(.07,.66,.8)*pleats*belly*side*.52*pulse;
                float rim=pow(1-abs(dot(n,view)),3);
                color+=float3(.1,.39,.5)*rim*.55;
                float scars=pow(saturate(.5+.5*sin(i.local.z*36+noise(i.local*7)*7)),32)*noise(i.local*4);
                color*=1-scars*.17;
                return float4(color,1);
            }
            ENDCG
        }
    }
}
