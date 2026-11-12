Shader "UnderAvatar/DetectionEdge"
{
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            ZTest Always ZWrite Off Cull Off
            Blend SrcAlpha OneMinusSrcAlpha
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            half4 Frag(Varyings input) : SV_Target
            {
                float2 uv=input.texcoord;
                float center=SAMPLE_TEXTURE2D_X(_BlitTexture,sampler_LinearClamp,uv).a;
                float inner=1;
                [unroll] for(int y=-1;y<=1;y++) [unroll] for(int x=-1;x<=1;x++)
                    inner=min(inner,SAMPLE_TEXTURE2D_X(_BlitTexture,sampler_PointClamp,uv+float2(x,y)*_BlitTexture_TexelSize.xy).a);
                return half4(1,.035,.015,saturate(center-inner));
            }
            ENDHLSL
        }
    }
}
