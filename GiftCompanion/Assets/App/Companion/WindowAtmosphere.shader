Shader "GiftCompanion/WindowAtmosphere" {
 Properties { _MainTex("Texture",2D)="white"{} _Strength("Strength",Float)=.6 }
 SubShader { Tags {"Queue"="Transparent" "RenderType"="Transparent"} Cull Off ZWrite Off Blend SrcAlpha OneMinusSrcAlpha
 Pass { CGPROGRAM
 #pragma vertex vert
 #pragma fragment frag
 #include "UnityCG.cginc"
 struct v2f {float4 pos:SV_POSITION;float2 uv:TEXCOORD0;};
 float _EffectTime,_Strength;float4 _Pane0,_Pane1,_Pane2,_Pane3,_Layers;
 v2f vert(appdata_img v){v2f o;o.pos=UnityObjectToClipPos(v.vertex);o.uv=v.texcoord;return o;}
 float edge(float2 a,float2 b,float2 p){return (b.x-a.x)*(p.y-a.y)-(b.y-a.y)*(p.x-a.x);}
 fixed4 frag(v2f i):SV_Target {
 float2 p=float2(i.uv.x*1280,(1-i.uv.y)*720);
 if(edge(_Pane0.xy,_Pane1.xy,p)<0||edge(_Pane1.xy,_Pane2.xy,p)<0||edge(_Pane2.xy,_Pane3.xy,p)<0||edge(_Pane3.xy,_Pane0.xy,p)<0)discard;
 float moon=exp(-pow((p.x-940+(p.y-200)*.18)/95,2))*.045;
 float sheer=pow(saturate(sin((p.x+sin(_EffectTime*.48)*2.5)*.035)),12)*.06;
 float alpha=moon*_Layers.x+sheer*_Layers.y;
 // Small stable star positions, with continuously varying brightness inside the same pane.
 float2 cell=floor(p/37);float seed=frac(sin(dot(cell,float2(127.1,311.7)))*43758.5453);
 float2 star=(cell+float2(.2+seed*.6,.3+frac(seed*17)*.4))*37;
 float twinkle=.35+.65*pow(.5+.5*sin(_EffectTime*(.55+seed)+seed*23),2);
 alpha+=exp(-dot(p-star,p-star)/1.5)*step(.86,seed)*twinkle*.16;
 for(int n=0;n<2;n++){float age=fmod(_EffectTime+float(n)*5.6,15)-3;float phase=saturate(age/1.1);float2 head=float2(1060-n*20,210-n*30)+float2(-75,32)*phase;float2 delta=p-head;float along=dot(delta,normalize(float2(26,-11)));float dist=abs(delta.x*.389+delta.y*.921);float streak=saturate(1-dist/1.2)*saturate(1-along/26)*step(0,along);alpha+=streak*sin(phase*3.14159)*.7*step(0,age)*step(age,1.1)*_Layers.z;}
 return float4(.88,.91,1,saturate(alpha)*_Strength);
 }
 ENDCG }
 }
}
