Shader "Astra/Setpieces/WhaleMembrane"
{
    Properties { _Clock("Animation clock",Float)=0 }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            struct v2f { float4 position:SV_POSITION; float2 uv:TEXCOORD0; float3 normal:TEXCOORD1; float3 world:TEXCOORD2; };
            float _Clock;
            v2f vert(appdata_base v)
            {
                v2f o; o.position=UnityObjectToClipPos(v.vertex); o.uv=v.texcoord.xy;
                o.normal=UnityObjectToWorldNormal(v.normal); o.world=mul(unity_ObjectToWorld,v.vertex).xyz; return o;
            }
            float4 frag(v2f i):SV_Target
            {
                float rib=pow(saturate(.5+.5*cos(i.uv.y*25.13274)),26);
                float branch=pow(saturate(.5+.5*cos(i.uv.y*80+i.uv.x*30+sin(i.uv.x*15)*2)),30);
                float edge=pow(saturate(i.uv.x),11);
                float ripple=.5+.5*sin(i.uv.x*18-_Clock*1.1);
                float rim=pow(1-abs(dot(normalize(i.normal),normalize(_WorldSpaceCameraPos-i.world))),2);
                float3 color=lerp(float3(.035,.19,.26),float3(.12,.65,.76),rib*.5+edge*.65+branch*.16);
                color+=float3(.015,.09,.12)*ripple+float3(.02,.08,.12)*rim;
                return float4(color,.24+rib*.24+edge*.32+branch*.13);
            }
            ENDCG
        }
    }
}
