Shader "Astra/MoltenInterior"
{
    Properties
    {
        _Color("Charred rock",Color)=(.075,.05,.038,1)
        _FragmentOrigin("Original object-space center",Vector)=(0,0,0,0)
        _Heat("Cooling mantle",Range(0,1))=1
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
            struct v2f { float4 pos:SV_POSITION; float3 normal:TEXCOORD0; float3 local:TEXCOORD1; };
            float4 _Color, _FragmentOrigin;
            float _Heat;
            v2f vert(appdata_base v)
            {
                v2f o; o.pos=UnityObjectToClipPos(v.vertex);
                o.normal=UnityObjectToWorldNormal(v.normal);
                o.local=v.vertex.xyz+_FragmentOrigin.xyz; return o;
            }
            float hash(float3 p) { return frac(sin(dot(p,float3(12.9898,78.233,37.719)))*43758.5453); }
            float noise(float3 p)
            {
                float3 f=frac(p), a=floor(p); f=f*f*(3-2*f);
                return lerp(lerp(lerp(hash(a),hash(a+float3(1,0,0)),f.x),lerp(hash(a+float3(0,1,0)),hash(a+float3(1,1,0)),f.x),f.y),lerp(lerp(hash(a+float3(0,0,1)),hash(a+float3(1,0,1)),f.x),lerp(hash(a+float3(0,1,1)),hash(a+1),f.x),f.y),f.z);
            }
            float4 frag(v2f i):SV_Target
            {
                float3 p=i.local;
                float broad=noise(p*34);
                float grain=noise(p*137)*.7+noise(p*291)*.3;
                float ridges=abs(noise(p*53+float3(1.7,2.1,4.8))-.5)*2;
                float rockLight=.38+saturate(dot(normalize(i.normal),normalize(float3(.3,.8,-.7))))*.62;
                float3 rock=_Color.rgb*(.42+grain*1.1+ridges*.7)*rockLight;
                // Only narrow connected fissures glow; most of the cut surface stays rough charcoal.
                float cracks=1-smoothstep(.006,.024,abs(broad-.515+noise(p*91)*.05));
                cracks*=smoothstep(.23,.69,noise(p*17+7));
                float pockets=smoothstep(.79,.91,noise(p*61))*smoothstep(.45,.7,broad);
                float glow=(cracks*.85+pockets*.45)*_Heat;
                float3 lava=lerp(float3(.48,.055,.005),float3(1.1,.31,.035),grain);
                return float4(rock+lava*glow,1);
            }
            ENDCG
        }
    }
}
