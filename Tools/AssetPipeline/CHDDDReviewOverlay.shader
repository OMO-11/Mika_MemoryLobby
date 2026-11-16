Shader "CHDDD/ReviewOverlay"
{
    Properties {
        _MainTex("Image",2D)="white"{} _Color("Color",Color)=(1,1,1,1)
        _Opacity("Opacity",Range(0,1))=0 _LinearTint("Blender linear tint",Vector)=(1,1,1,1)
        _ApplyLinearTint("Apply source brow tint",Float)=0
    }
    SubShader {
        Tags {"RenderType"="Transparent" "Queue"="Transparent+10"}
        Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
        ZWrite Off Cull Off
        Pass {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _MainTex;fixed4 _Color;float4 _LinearTint;float _Opacity,_ApplyLinearTint;
            struct v2f{float4 pos:SV_POSITION;float2 uv:TEXCOORD0;};
            v2f vert(appdata_base v){v2f o;o.pos=UnityObjectToClipPos(v.vertex);o.uv=v.texcoord.xy;return o;}
            fixed4 frag(v2f i):SV_Target {
                fixed4 c=tex2D(_MainTex,i.uv);
                if(_ApplyLinearTint>.5)c.rgb=LinearToGammaSpace(GammaToLinearSpace(c.rgb)*_LinearTint.rgb);
                c.rgb*=_Color.rgb;c.a*=_Color.a*_Opacity;
                return c;
            }
            ENDCG
        }
    }
}
