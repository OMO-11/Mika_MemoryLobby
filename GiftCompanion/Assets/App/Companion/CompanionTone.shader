Shader "Hidden/GiftCompanion/CompanionTone"
{
    Properties { _MainTex("Scene", 2D) = "white" {} _Exposure("Exposure", Float) = .95 _Compression("Highlight compression", Float) = .35 }
    SubShader { Cull Off ZWrite Off ZTest Always
        Pass {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _MainTex;
            float _Exposure, _Compression;
            float4 frag(v2f_img input) : SV_Target {
                float4 color = tex2D(_MainTex, input.uv);
                float3 exposed = max(0, color.rgb * _Exposure);
                color.rgb = exposed / (1 + max(0, exposed - .75) * _Compression);
                return color;
            }
            ENDCG
        }
    }
}
