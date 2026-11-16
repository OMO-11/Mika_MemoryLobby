// Built-in/Gamma scene adapter for straight-alpha atlases and Spine PMA vertex colors.
// No runtime shader-feature variant is required; Normal/Additive use the same blend.
Shader "GiftCompanion/SpineStraightAlpha"
{
    Properties { _MainTex("Atlas", 2D) = "white" {} _InputIsStraight("Straight Alpha Input", Float) = 1 _VisualBlur("Near pass blur", Float) = 0 }
    SubShader {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" }
        Fog { Mode Off }
        Cull Off ZWrite Off Blend One OneMinusSrcAlpha
        Pass {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _MainTex;
            float _InputIsStraight;
            float _VisualBlur; float4 _MainTex_TexelSize;
            struct Input { float4 vertex : POSITION; float2 uv : TEXCOORD0; float4 color : COLOR; };
            struct Output { float4 position : SV_POSITION; float2 uv : TEXCOORD0; float4 color : COLOR; };
            Output vert(Input input) { Output output; output.position = UnityObjectToClipPos(input.vertex); output.uv = input.uv; output.color = input.color; return output; }
            float4 frag(Output input) : SV_Target {
                float4 textureColor = tex2D(_MainTex, input.uv);
                if (_VisualBlur > .001) { float2 d = _MainTex_TexelSize.xy * _VisualBlur * 2; textureColor = (textureColor*4 + tex2D(_MainTex,input.uv+d)+tex2D(_MainTex,input.uv-d)+tex2D(_MainTex,input.uv+float2(d.x,-d.y))+tex2D(_MainTex,input.uv+float2(-d.x,d.y)))/8; }
                textureColor.rgb *= lerp(1.0, textureColor.a, saturate(_InputIsStraight));
                return textureColor * input.color;
            }
            ENDCG
        }
    }
}
