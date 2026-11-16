Shader "CHDDD/Toon"
{
    Properties
    {
        _MainTex("Color", 2D) = "white" {}
        _MaskTex("Mask", 2D) = "white" {}
        _SpecTex("Hair highlight", 2D) = "black" {}
        _Color("Tint", Color) = (1,1,1,1)
        _ShadowColor("Shadow", Color) = (.8,.72,.82,1)
        _Threshold("Shadow threshold", Range(0,1)) = .45
        _SpecStrength("Highlight strength", Range(0,1)) = 0
        _Unlit("Unlit", Float) = 0
        _Emission("Emission", Float) = 1
        _OutlineColor("Outline", Color) = (.28,.20,.32,1)
        _OutlineWidth("Outline width", Float) = .0008
        _Cull("Cull", Float) = 0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        Pass
        {
            Name "OUTLINE"
            Cull Front
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            float _OutlineWidth; fixed4 _OutlineColor;
            struct v2f { float4 pos:SV_POSITION; };
            v2f vert(appdata_base v) { v2f o; v.vertex.xyz += normalize(v.normal)*_OutlineWidth; o.pos=UnityObjectToClipPos(v.vertex); return o; }
            fixed4 frag(v2f i):SV_Target { return _OutlineColor; }
            ENDCG
        }
        Pass
        {
            Tags { "LightMode"="ForwardBase" }
            Cull [_Cull]
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            #include "Lighting.cginc"
            sampler2D _MainTex, _MaskTex, _SpecTex;
            float4 _MainTex_ST; fixed4 _Color, _ShadowColor;
            float _Threshold, _SpecStrength, _Unlit, _Emission;
            struct v2f { float4 pos:SV_POSITION; float2 uv:TEXCOORD0; float3 normal:TEXCOORD1; };
            v2f vert(appdata_base v) { v2f o; o.pos=UnityObjectToClipPos(v.vertex); o.uv=TRANSFORM_TEX(v.texcoord,_MainTex); o.normal=UnityObjectToWorldNormal(v.normal); return o; }
            fixed4 frag(v2f i):SV_Target {
                float light=dot(normalize(i.normal),normalize(_WorldSpaceLightPos0.xyz))*.5+.5;
                float threshold=_Threshold + (tex2D(_MaskTex,i.uv).g-.5)*.08;
                fixed3 shade=lerp(_ShadowColor.rgb,float3(1,1,1),step(threshold,light));
                shade=lerp(shade,float3(1,1,1),saturate(_Unlit));
                fixed3 color=tex2D(_MainTex,i.uv).rgb*_Color.rgb*shade;
                color+=tex2D(_SpecTex,i.uv).rgb*_SpecStrength*.18;
                return fixed4(color*_Emission,1);
            }
            ENDCG
        }
    }
    FallBack "Unlit/Texture"
}
