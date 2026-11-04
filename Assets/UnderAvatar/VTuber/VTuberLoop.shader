Shader "UnderAvatar/VTuberLoop"
{
    Properties
    {
        [PerRendererData] _MainTex ("Expression Atlas", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _Blink ("Blink", Range(0,1)) = 0
        _Talk ("Talking", Range(0,1)) = 0
        _EyeRect ("Eye region minX,minY,maxX,maxY", Vector) = (.28,.48,.73,.63)
        _MouthRect ("Mouth region", Vector) = (.40,.35,.65,.45)
        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
    }
    SubShader
    {
        Tags {"Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "PreviewType"="Plane" "CanUseSpriteAtlas"="False"}
        Stencil {Ref [_Stencil] Comp [_StencilComp] Pass [_StencilOp] ReadMask [_StencilReadMask] WriteMask [_StencilWriteMask]}
        Cull Off Lighting Off ZWrite Off ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"
            struct appdata {float4 vertex:POSITION;float4 color:COLOR;float2 uv:TEXCOORD0;};
            struct v2f {float4 vertex:SV_POSITION;fixed4 color:COLOR;float2 uv:TEXCOORD0;float4 world:TEXCOORD1;};
            sampler2D _MainTex;
            fixed4 _Color;
            float _Blink,_Talk;
            float4 _EyeRect,_MouthRect,_ClipRect;
            v2f vert(appdata v)
            {v2f o;o.world=v.vertex;o.vertex=UnityObjectToClipPos(v.vertex);o.uv=v.uv;o.color=v.color*_Color;return o;}
            float region(float2 uv,float4 r)
            {float2 edge=min(uv-r.xy,r.zw-uv);return smoothstep(0,.022,min(edge.x,edge.y));}
            fixed4 frag(v2f i):SV_Target
            {
                float2 uv=clamp(i.uv,.001,.999)*.5;
                fixed4 idle=tex2D(_MainTex,uv+float2(0,.5));
                fixed4 closed=tex2D(_MainTex,uv+float2(.5,.5));
                fixed4 speaking=tex2D(_MainTex,uv);
                fixed4 both=tex2D(_MainTex,uv+float2(.5,0));
                float b=_Blink*region(i.uv,_EyeRect),t=_Talk*region(i.uv,_MouthRect);
                fixed4 result=lerp(lerp(idle,closed,b),lerp(speaking,both,b),t)*i.color;
                // Preserve the original cutout silhouette at expression boundaries.
                result.a=idle.a*i.color.a;
                #ifdef UNITY_UI_CLIP_RECT
                result.a*=UnityGet2DClipping(i.world.xy,_ClipRect);
                #endif
                #ifdef UNITY_UI_ALPHACLIP
                clip(result.a-.001);
                #endif
                return result;
            }
            ENDCG
        }
    }
}
