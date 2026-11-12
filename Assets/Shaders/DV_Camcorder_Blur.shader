Shader "UnderAvatar/DV Camcorder Blur"
{
    Properties
    {
        [PerRendererData] _MainTex ("Input (RawImage)", 2D) = "white" {}
        _TargetResolution ("Target resolution", Vector) = (520,292,0,0)
        _BlurRadius ("Soft blur", Range(0,3)) = 1.25
        _ChromaOffset ("Chroma bleed", Range(0,0.02)) = 0.0045
        _Overexposure ("Highlight clipping", Range(1,3)) = 1.45
        _ColorSteps ("Color steps", Range(4,64)) = 14
        _NoiseIntensity ("Shadow noise", Range(0,0.2)) = 0.05
        _VignetteIntensity ("Vignette", Range(0,1)) = 0.55
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" }
        Cull Off ZWrite Off ZTest Always
        Pass
        {
            HLSLPROGRAM
            #pragma vertex DVVertex
            #pragma fragment DVFragment
            #pragma multi_compile_local_fragment _ DV_BLIT
            #pragma multi_compile_local_vertex _ DV_BLIT
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            TEXTURE2D(_MainTex);
            CBUFFER_START(UnityPerMaterial)
                float4 _TargetResolution;
                float _BlurRadius, _ChromaOffset, _Overexposure, _ColorSteps;
                float _NoiseIntensity, _VignetteIntensity;
            CBUFFER_END

            struct DVAttributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                uint vertexID : SV_VertexID;
            };

            Varyings DVVertex(DVAttributes input)
            {
                #if defined(DV_BLIT)
                    Attributes vertex = (Attributes)0;
                    vertex.vertexID = input.vertexID;
                    return Vert(vertex);
                #else
                    Varyings output = (Varyings)0;
                    output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                    output.texcoord = input.uv;
                    return output;
                #endif
            }

            float3 Signal(float2 uv)
            {
                #if defined(DV_BLIT)
                    return SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, saturate(uv)).rgb;
                #else
                    return SAMPLE_TEXTURE2D(_MainTex, sampler_LinearClamp, saturate(uv)).rgb;
                #endif
            }

            half4 DVFragment(Varyings input) : SV_Target
            {
                float2 uv = input.texcoord;
                float2 stepUV = _BlurRadius / max(_TargetResolution.xy, 1.0);
                float3 signal = Signal(uv) * 0.4;
                signal += (Signal(uv + float2(stepUV.x,0)) + Signal(uv - float2(stepUV.x,0))
                         + Signal(uv + float2(0,stepUV.y)) + Signal(uv - float2(0,stepUV.y))) * 0.15;

                float2 bleed = float2(_ChromaOffset, 0);
                signal.r = lerp(signal.r, 0.7 * Signal(uv + bleed).r + 0.3 * Signal(uv + 2 * bleed).r, 0.45);
                signal.b = lerp(signal.b, 0.7 * Signal(uv - bleed).b + 0.3 * Signal(uv - 2 * bleed).b, 0.45);

                float brightness = dot(signal, float3(0.2126, 0.7152, 0.0722));
                signal *= lerp(1, _Overexposure, smoothstep(0.25, 0.85, brightness));
                float3 displayColor = LinearToSRGB(saturate(signal));
                displayColor = floor(displayColor * _ColorSteps) / _ColorSteps;

                float2 cell = floor(uv * _TargetResolution.xy);
                float frame = floor(_Time.y * 23.976);
                float noise = frac(sin(dot(cell + frame * float2(0.37, 1.73), float2(12.9898,78.233))) * 43758.5453);
                displayColor += (noise - 0.4) * _NoiseIntensity * (1 - smoothstep(0.03, 0.42, brightness));
                float edge = smoothstep(0.30, 1.35, length(uv * 2 - 1));
                return half4(SRGBToLinear(saturate(displayColor * (1 - edge * _VignetteIntensity))), 1);
            }
            ENDHLSL
        }
    }
}
