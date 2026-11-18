Shader "Western/SanctuaryFloor"
{
    Properties { _Tint ("Tint", Color) = (1,1,1,1) }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off ZWrite Off ZTest Always
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            float4 _Tint, _Shape, _Room, _Lights, _LightRadii, _EchoCourt;
            float _EchoPreview;
            float _DeathRay, _Pulse, _ElectricField, _Ricochet;
            struct Data { float4 position : SV_POSITION; float2 p : TEXCOORD0; float2 world : TEXCOORD1; };
            Data vert(appdata_base v)
            {
                Data o;
                o.position = UnityObjectToClipPos(v.vertex);
                o.p = v.vertex.xy * 2;
                o.world = mul(unity_ObjectToWorld, v.vertex).xz;
                return o;
            }
            float4 frag(Data i) : SV_Target
            {
                clip(_Room.zw - abs(i.world - _Room.xy));
                if (_EchoCourt.x>0)
                {
                    float2 local=i.world-_Room.xy;
                    clip(_EchoCourt.x-length(float2(local.x,max(0,abs(local.y)-_EchoCourt.y))));
                }
                if (_Shape.x > 2.5)
                {
                    float shade = smoothstep(_LightRadii.x-.60,_LightRadii.x+.60,distance(i.world,_Lights.xy));
                    if (_LightRadii.y > 0)
                        shade = min(shade,smoothstep(_LightRadii.y-.75,_LightRadii.y+.75,distance(i.world,_Lights.zw)));
                    return float4(_Tint.rgb,_Tint.a*shade);
                }
                float r = length(i.p);
                float edge;
                if (_Shape.x > 1.5)
                {
                    float bow = .23 - .58*i.p.y*i.p.y;
                    float thick = .035 + .10*(1-i.p.y*i.p.y);
                    float band = abs(i.p.x*.6-bow)/thick;
                    clip(1.9-band);
                    float core = 1-smoothstep(.12,.98,band);
                    return float4(lerp(float3(1,.58,.17),float3(1,1,.88),core),
                        (1-smoothstep(.82,1.9,band))*saturate((1-abs(i.p.y))*15));
                }
                if (_Shape.x > .5) edge = min(1-abs(i.p.x), 1-abs(i.p.y));
                else
                {
                    clip(1-r);
                    clip(r-_Shape.y);
                    if (_Shape.z > -.9999 && r > .0001) clip(i.p.x/r-_Shape.z);
                    edge = _Shape.y > 0 ? min(1-r,r-_Shape.y) : 1-r;
                }
                float4 c = _Tint;
                if (_Ricochet>.5)
                {
                    float turn=atan2(i.p.y,i.p.x)-_Time.y*17.1;
                    float sweep=frac(turn*.31831+r*.36);
                    float bladeTrail=smoothstep(.58,.98,sweep)*smoothstep(.16,.82,r);
                    float rim=smoothstep(.965,.985,r);
                    return float4(lerp(c.rgb,float3(1,.82,1),max(bladeTrail,rim)),
                        saturate(c.a*.66+bladeTrail*.34+rim*.38));
                }
                if (_ElectricField>.5)
                {
                    float theta=atan2(i.p.y,i.p.x);
                    float jitter=sin(r*25+floor(_Time.y*9))*.32+sin(r*61)*.11;
                    float arc=pow(saturate(.5+.5*cos(theta*7+jitter*5+floor(_Time.y*5)*1.3)),20);
                    float crest=1-smoothstep(.012,.07,abs(r-frac(_Time.y*.85)));
                    return float4(lerp(c.rgb,float3(.90,.80,1),max(arc,crest)),c.a+max(arc,crest)*.4);
                }
                if (_DeathRay>.5)
                {
                    float angle=atan2(i.p.y,max(i.p.x,.0001));
                    float fan=saturate(abs(angle)/.2618);
                    float strands=pow(.5+.5*cos(angle*145+sin(r*46-_Pulse*43)*.8),4);
                    float core=(1-smoothstep(0,.22,fan))*.82+strands*.45;
                    return float4(lerp(float3(.45,.075,.83),float3(1,.91,1),saturate(core)),
                        .74+.22*(1-fan));
                }
                if (_EchoPreview>.5)
                {
                    if (_Shape.x<.5 && _Shape.z>-.9999) edge=min(edge,i.p.x-r*_Shape.z);
                    float rim=1-smoothstep(.005,.033,edge);
                    return float4(lerp(c.rgb,float3(.79,.95,1),rim),lerp(c.a,.95,rim));
                }
                if (_Shape.w > 0) c.a *= smoothstep(0,_Shape.w,edge);
                else c.a = saturate(c.a + (1-smoothstep(0,.035,edge)) * .22);
                return c;
            }
            ENDCG
        }
    }
}

