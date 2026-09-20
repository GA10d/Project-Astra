Shader "Astra/Setpieces/CrimsonEye"
{
    Properties
    {
        _Color("Flesh",Color)=(.22,.042,.058,1)
        _FleshOnly("Flesh only",Float)=0
        _Reveal("Arrival",Range(0,1))=1
        _Clock("Local animation clock",Float)=0
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
            float4 _Color; float _FleshOnly, _Reveal, _Clock;
            v2f vert(appdata_base v) { v2f o; o.pos=UnityObjectToClipPos(v.vertex); o.p=v.vertex.xyz; o.normal=UnityObjectToWorldNormal(v.normal); o.world=mul(unity_ObjectToWorld,v.vertex).xyz; return o; }
            float hash(float3 p) { return frac(sin(dot(p,float3(17.73,43.12,97.91)))*43758.54); }
            float noise(float3 p)
            {
                float3 a=floor(p), f=frac(p); f=f*f*(3-2*f);
                return lerp(lerp(lerp(hash(a),hash(a+float3(1,0,0)),f.x),lerp(hash(a+float3(0,1,0)),hash(a+float3(1,1,0)),f.x),f.y),lerp(lerp(hash(a+float3(0,0,1)),hash(a+float3(1,0,1)),f.x),lerp(hash(a+float3(0,1,1)),hash(a+1),f.x),f.y),f.z);
            }
            float4 frag(v2f i):SV_Target
            {
                float3 n=normalize(i.normal), v=normalize(_WorldSpaceCameraPos-i.world);
                float lit=.35+.7*saturate(dot(n,normalize(float3(-.65,.6,-.3))));
                float veins=pow(1-saturate(abs(noise(i.p*26)-.5)*13),4);
                float3 flesh=_Color.rgb*(.68+noise(i.p*12)*.65)*lit;
                flesh=lerp(flesh,float3(.06,.007,.017),veins*.65);
                float3 sphere=normalize(i.p);
                float r=length(sphere.yz), theta=atan2(sphere.y,sphere.z);
                float iris=1-smoothstep(.66,.69,r);
                float front=smoothstep(.3,.65,-sphere.x);
                float streaks=.5+.5*sin(theta*98+noise(sphere*20)*7+r*22);
                float3 irisColor=lerp(float3(.22,.085,.014),float3(1.05,.35,.032),streaks*.65+.15);
                irisColor*=.6+.4*sin(r*32+streaks*2);
                float slit=abs(sphere.z)/(.038+.04*sqrt(saturate(1-pow(sphere.y/.64,2))));
                float pupil=(1-smoothstep(.8,1.3,slit))*(1-smoothstep(.54,.61,abs(sphere.y)));
                irisColor=lerp(irisColor,float3(.002,.001,.006),pupil);
                float rim=1-smoothstep(.025,.045,abs(r-.674));
                irisColor=lerp(irisColor,float3(.028,.004,.012),rim);
                float3 sclera=lerp(float3(.36,.19,.17),float3(.12,.012,.029),veins*.72)*lit;
                float3 eye=lerp(sclera,irisColor,iris*front);
                float shine=pow(saturate(dot(reflect(-normalize(float3(-.65,.6,-.3)),n),v)),48)*.38;
                float3 c=lerp(eye,flesh,saturate(_FleshOnly))+float3(.32,.17,.13)*shine;
                return float4(c*_Reveal,1);
            }
            ENDCG
        }
    }
}
