Shader "CHDDD/ReviewFace"
{
    Properties {
        _MainTex("Color",2D)="white"{} _MaskTex("Mask",2D)="white"{} _SpecTex("Spec",2D)="black"{}
        _Color("Tint",Color)=(1,1,1,1) _ShadowColor("Shadow",Color)=(1,.788,.788,1)
        _Threshold("Threshold",Float)=.45 _SpecStrength("Spec",Float)=0 _Unlit("Unlit",Float)=0 _Emission("Emission",Float)=1
        _OutlineWidth("Outline",Float)=.0008 _OutlineColor("Outline color",Color)=(.28,.2,.32,1) _Cull("Cull",Float)=0
        _ReviewEyeMask("Replace old closed eye art",Range(0,1))=0
        _ReviewFaceNormal("Head forward normal",Vector)=(0,0,-1,0)
    }
    SubShader {
        Tags {"RenderType"="Opaque" "Queue"="Geometry"}
        Pass {
            Cull Front
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            float _OutlineWidth,_ReviewEyeMask;fixed4 _OutlineColor;
            struct input{float4 vertex:POSITION;float3 normal:NORMAL;float4 masks:COLOR;};
            struct v2f {float4 pos:SV_POSITION;float mask:TEXCOORD0;};
            v2f vert(input v){v2f o;v.vertex.xyz+=normalize(v.normal)*_OutlineWidth;o.pos=UnityObjectToClipPos(v.vertex);o.mask=v.masks.r;return o;}
            fixed4 frag(v2f i):SV_Target{clip(.005-i.mask*_ReviewEyeMask);return _OutlineColor;}
            ENDCG
        }
        Pass {
            Tags {"LightMode"="ForwardBase"} Cull [_Cull]
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            #include "Lighting.cginc"
            sampler2D _MainTex,_MaskTex;fixed4 _Color,_ShadowColor;float4 _ReviewFaceNormal;float _Threshold,_Unlit,_Emission,_ReviewEyeMask;
            struct input {float4 vertex:POSITION;float3 normal:NORMAL;float2 uv:TEXCOORD0;float4 masks:COLOR;};
            struct v2f {float4 pos:SV_POSITION;float2 uv:TEXCOORD0;float3 normal:TEXCOORD1;float2 masks:TEXCOORD2;};
            v2f vert(input v){v2f o;o.pos=UnityObjectToClipPos(v.vertex);o.uv=v.uv;o.normal=UnityObjectToWorldNormal(v.normal);o.masks=v.masks.rg;return o;}
            fixed4 frag(v2f i):SV_Target {
                float3 normal=normalize(lerp(i.normal,_ReviewFaceNormal.xyz,i.masks.r*_ReviewEyeMask));
                float light=dot(normal,normalize(_WorldSpaceLightPos0.xyz))*.5+.5;
                float threshold=_Threshold+(tex2D(_MaskTex,i.uv).g-.5)*.08;
                fixed3 shade=lerp(_ShadowColor.rgb,1,step(threshold,light));shade=lerp(shade,1,saturate(_Unlit));
                fixed3 skin=tex2D(_MainTex,float2(.967,.039)).rgb;
                float replacement=i.masks.r*_ReviewEyeMask;
                fixed3 color=lerp(tex2D(_MainTex,i.uv).rgb,skin,replacement)*_Color.rgb*shade;
                float highlight=saturate((dot(shade,float3(.299,.587,.114))-.9)/.08);
                // Port the V14 seam cleanup only inside the existing closed-lid mask.
                fixed3 flatSkin=LinearToGammaSpace(GammaToLinearSpace(skin)*float3(1,.85,.85));
                color=lerp(color,flatSkin,replacement*i.masks.g*highlight);
                return fixed4(color*_Emission,1);
            }
            ENDCG
        }
    }
}
