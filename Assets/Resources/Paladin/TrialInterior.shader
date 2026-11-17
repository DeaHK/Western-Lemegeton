Shader "Western/Trial Interior"
{
    Properties
    {
        _MainTex ("Day", 2D) = "white" {}
        _DuskTex ("Dusk", 2D) = "white" {}
        _Dusk ("Transition", Range(0,1)) = 0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "DisableBatching"="True" }
        Pass
        {
            Cull Off ZWrite Off ZTest Always
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _MainTex, _DuskTex;
            float3 _Eye, _Up, _Forward;
            float _Dusk, _VerticalOffset;
            float2 _Extent;
            float4 _ParallelMap, _HunterCourt, _EchoCourt;
            struct paintedVertex { float4 position : SV_POSITION; float3 projection : TEXCOORD0; float3 room : TEXCOORD1; };
            paintedVertex vert(float4 vertex : POSITION)
            {
                paintedVertex o;
                o.position = UnityObjectToClipPos(vertex);
                o.room = vertex.xyz;
                float3 ray = vertex.xyz - _Eye;
                // Reference painting has a 16:9 aspect and a 48 degree vertical field of view.
                o.projection = float3(ray.x * .6317, dot(ray,_Up) * 1.123, dot(ray,_Forward));
                return o;
            }
            fixed4 frag(paintedVertex i) : SV_Target
            {
                float2 uv = i.projection.xy / max(.01,i.projection.z) + float2(.5,.5 + _VerticalOffset);
                if (_ParallelMap.w > .5)
                    uv = float2(.5 + i.room.x * _ParallelMap.x, _ParallelMap.z + dot(i.room,_Up) * _ParallelMap.y);
                // Repeat the architecture symmetrically beyond its painted lateral extent.
                uv.x = 1 - abs(frac(uv.x * .5) * 2 - 1);
                // Beyond the canvas, mirror a narrow strip of the nearest floor
                // or window texture. Clamping a single row produces a streak.
                float outside = max(-uv.y, uv.y-1);
                float strip = .20 * (1 - abs(frac(max(0,outside)/.40) * 2 - 1));
                if (uv.y < 0) uv.y = strip;
                if (uv.y > 1) uv.y = 1 - strip;
                fixed4 colour = lerp(tex2D(_MainTex,uv), tex2D(_DuskTex,uv), _Dusk);
                if (_ParallelMap.w>.5 && _HunterCourt.z>.5 && i.room.y<.01)
                {
                    float2 margin=_Extent-abs(i.room.xz);
                    float edge=abs(min(margin.x,margin.y));
                    float nearby=1-smoothstep(.4,3.2,distance(i.room.xz,_HunterCourt.xy));
                    float glint=(1-smoothstep(.05,.62,edge))*nearby*(1-_EchoCourt.z);
                    colour.rgb=lerp(colour.rgb,float3(1,.83,.48),glint*.52);
                    if (_EchoCourt.z>0)
                    {
                        float2 side=float2(i.room.x,max(0,abs(i.room.z)-_EchoCourt.y));
                        float d=length(side)-_EchoCourt.x;
                        colour.rgb*=1-.42*smoothstep(-.02,.7,d)*_EchoCourt.z;
                        colour.rgb=lerp(colour.rgb,float3(1,.90,.62),(1-smoothstep(.04,.20,abs(d)))*_EchoCourt.z*.85);
                    }
                }
                if (_ParallelMap.w<.5 && i.room.y<.01)
                {
                    float2 margin=_Extent-abs(i.room.xz);
                    float edge=min(margin.x,margin.y);
                    float rule=1-smoothstep(.025,.025+max(.015,fwidth(edge)),min(abs(edge),abs(edge-.16)));
                    colour.rgb=lerp(colour.rgb,float3(.40,.25,.11),rule*.64);
                }
                return colour;
            }
            ENDCG
        }
    }
}
