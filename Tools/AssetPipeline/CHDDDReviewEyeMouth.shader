Shader "CHDDD/ReviewEyeMouth"
{
    Properties {_MainTex("Color",2D)="white"{} _ReviewEyeMask("Hide old eye strips",Range(0,1))=0}
    SubShader {
        Tags {"RenderType"="Transparent" "Queue"="AlphaTest"}
        Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
        ZWrite On Cull Off
        Pass {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _MainTex;float _ReviewEyeMask;
            struct input{float4 vertex:POSITION;float2 uv:TEXCOORD0;float4 mask:COLOR;};
            struct v2f{float4 pos:SV_POSITION;float2 uv:TEXCOORD0;float mask:TEXCOORD1;};
            v2f vert(input v){v2f o;o.pos=UnityObjectToClipPos(v.vertex);o.uv=v.uv;o.mask=v.mask.b;return o;}
            fixed4 frag(v2f i):SV_Target{fixed4 c=tex2D(_MainTex,i.uv);c.a=1-i.mask*_ReviewEyeMask;clip(c.a-.001);return c;}
            ENDCG
        }
    }
}
