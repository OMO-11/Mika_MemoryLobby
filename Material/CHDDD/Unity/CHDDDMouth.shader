Shader "CHDDD/MouthAtlas"
{
    Properties
    {
        _MainTex("Mouth atlas", 2D) = "white" {}
        _MouthTile("Tile: row * 100 + column", Float) = 300
        _MouthBlendEnabled("Blend atlas cells", Float) = 0
        _MouthTiles0("Atlas cells A", Vector) = (300,0,0,0)
        _MouthTiles1("Atlas cells B", Vector) = (0,0,0,0)
        _MouthWeights0("Atlas weights A", Vector) = (1,0,0,0)
        _MouthWeights1("Atlas weights B", Vector) = (0,0,0,0)
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" }
        Blend One OneMinusSrcAlpha
        ZWrite Off
        Cull Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"
            sampler2D _MainTex; float _MouthTile, _MouthBlendEnabled;
            float4 _MouthTiles0,_MouthTiles1,_MouthWeights0,_MouthWeights1;
            struct v2f { float4 pos:SV_POSITION; float2 uv:TEXCOORD0; };
            v2f vert(appdata_base v) { v2f o; o.pos=UnityObjectToClipPos(v.vertex); o.uv=v.texcoord.xy; return o; }
            float4 cell(float2 uv,float tile) {
                float row=floor(tile/100), col=tile-row*100;
                float4 c=tex2D(_MainTex,uv*.5+float2(col,row)/8);
                return float4(c.rgb*c.a,c.a);
            }
            fixed4 frag(v2f i):SV_Target {
                if (_MouthBlendEnabled<.5) return cell(i.uv,_MouthTile);
                float4 result=0;
                [unroll] for(int k=0;k<4;k++) {
                    if(_MouthWeights0[k]>.000001) result+=cell(i.uv,_MouthTiles0[k])*_MouthWeights0[k];
                    if(_MouthWeights1[k]>.000001) result+=cell(i.uv,_MouthTiles1[k])*_MouthWeights1[k];
                }
                return result;
            }
            ENDCG
        }
    }
}
