Shader "Astra/Setpieces/CrimsonNebula"
{
    Properties { _Reveal("Arrival",Range(0,1))=1 _Clock("Local animation clock",Float)=0 }
    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Opaque" }
        Cull Off ZWrite Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            struct v2f { float4 pos:SV_POSITION; float3 p:TEXCOORD0; };
            float _Reveal, _Clock;
            v2f vert(appdata_base v) { v2f o; o.pos=UnityObjectToClipPos(v.vertex); o.p=normalize(v.vertex.xyz); return o; }
            float hash(float3 p) { return frac(sin(dot(p,float3(29.2,17.8,95.9)))*43758.54); }
            float noise(float3 p)
            {
                float3 a=floor(p), f=frac(p); f=f*f*(3-2*f);
                return lerp(lerp(lerp(hash(a),hash(a+float3(1,0,0)),f.x),lerp(hash(a+float3(0,1,0)),hash(a+float3(1,1,0)),f.x),f.y),lerp(lerp(hash(a+float3(0,0,1)),hash(a+float3(1,0,1)),f.x),lerp(hash(a+float3(0,1,1)),hash(a+1),f.x),f.y),f.z);
            }
            float4 frag(v2f i):SV_Target
            {
                float3 p=normalize(i.p)*5.5+float3(0,_Clock*.003,0);
                float n=noise(p)+noise(p*2.1)*.47+noise(p*4.3)*.2;
                float vein=pow(saturate(1-abs(n-.8)*2.2),3);
                float3 c=lerp(float3(.002,.001,.008),float3(.16,.005,.014),vein);
                c+=float3(.045,.003,.011)*pow(noise(p*13),5);
                float3 cells=floor(normalize(i.p)*430);
                float stars=step(.9988,hash(cells))*pow(saturate(1-length(frac(normalize(i.p)*430)-.5)*2),4);
                c+=float3(.7,.32,.18)*stars;
                return float4(c*_Reveal,1);
            }
            ENDCG
        }
    }
}
