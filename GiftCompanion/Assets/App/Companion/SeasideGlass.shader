Shader "GiftCompanion/SeasideGlass" {
 Properties { _MainTex("Scene",2D)="white"{} _Blur("Menu",Float)=0 _Flash("Entrance",Float)=0 }
 SubShader { Cull Off ZWrite Off ZTest Always Pass { CGPROGRAM
 #pragma vertex vert_img
 #pragma fragment frag
 #include "UnityCG.cginc"
 sampler2D _MainTex;float4 _MainTex_TexelSize;float _Blur,_Flash;
 fixed4 frag(v2f_img i):SV_Target {
 float2 d=_MainTex_TexelSize.xy*3*_Blur;
 fixed4 c=tex2D(_MainTex,i.uv)*4;
 c+=tex2D(_MainTex,i.uv+d)+tex2D(_MainTex,i.uv-d)+tex2D(_MainTex,i.uv+float2(d.x,-d.y))+tex2D(_MainTex,i.uv+float2(-d.x,d.y));c/=8;
 c.rgb=lerp(c.rgb,1,saturate(_Flash));c.rgb=lerp(c.rgb,float3(.09,.11,.20),_Blur*.28);return c;
 }
 ENDCG } }
}
