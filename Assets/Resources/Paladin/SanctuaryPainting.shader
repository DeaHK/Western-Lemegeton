Shader "Western/SanctuaryPainting"
{
    Properties
    {
        [PerRendererData] _MainTex ("Painting", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        [HideInInspector] _RendererColor ("Renderer Color", Color) = (1,1,1,1)
        [HideInInspector] _Flip ("Flip", Vector) = (1,1,1,1)
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "CanUseSpriteAtlas"="False" }
        Cull Off ZWrite Off Blend One OneMinusSrcAlpha
        Pass
        {
            CGPROGRAM
            #pragma vertex SpriteVert
            #pragma fragment frag
            #include "UnitySprites.cginc"
            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 c = SampleSpriteTexture(i.texcoord) * i.color;
                float2 border = min(i.texcoord,1-i.texcoord);
                c.a *= smoothstep(0,.028,border.x) * smoothstep(0,.044,border.y);
                c.rgb *= c.a;
                return c;
            }
            ENDCG
        }
    }
}

