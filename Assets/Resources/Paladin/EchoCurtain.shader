Shader "Western/Echo Curtain"
{
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "DisableBatching"="True" }
        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off ZWrite Off ZTest Always
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            float _Strength;
            struct Vertex { float4 position : SV_POSITION; float3 local : TEXCOORD0; };
            Vertex vert(float4 p : POSITION)
            {
                Vertex v; v.position=UnityObjectToClipPos(p); v.local=p.xyz; return v;
            }
            fixed4 frag(Vertex v) : SV_Target
            {
                float h=saturate((v.local.y-.04)/1.55);
                float ray=.5+.5*cos(v.local.x*8+v.local.z*9+sin(_Time.y*2));
                float foot=1-smoothstep(0,.09,h);
                float veil=(.20+.36*pow(ray,6))*(1-h)*(1-h);
                return float4(lerp(float3(.98,.58,.15),float3(1,.97,.75),foot),_Strength*(foot*.8+veil));
            }
            ENDCG
        }
    }
}
