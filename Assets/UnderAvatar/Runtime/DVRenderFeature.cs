using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Rendering.Universal;

namespace UnderAvatar
{
    public sealed class DVRenderFeature : ScriptableRendererFeature
    {
        public Shader EffectShader;
        sealed class CamcorderPass : ScriptableRenderPass
        {
            public DVCamcorderDisplay Display;

            public CamcorderPass()
            {
                renderPassEvent = RenderPassEvent.AfterRenderingPostProcessing;
                requiresIntermediateTexture = true;
            }

            public override void RecordRenderGraph(RenderGraph graph, ContextContainer frame)
            {
                var resources = frame.Get<UniversalResourceData>();
                if (resources.isActiveTargetBackBuffer) return;

                var source = resources.activeColorTexture;
                var descriptor = graph.GetTextureDesc(source);
                descriptor.clearBuffer = false;
                descriptor.msaaSamples = MSAASamples.None;
                descriptor.depthBufferBits = DepthBits.None;
                descriptor.name = "DV processed camera color";
                var output = graph.CreateTexture(descriptor);
                descriptor.width = Display.Resolution.x;
                descriptor.height = Display.Resolution.y;
                descriptor.filterMode = FilterMode.Bilinear;
                descriptor.name = "DV low resolution signal";
                var low = graph.CreateTexture(descriptor);

                graph.AddBlitPass(source, low, Vector2.one, Vector2.zero, passName: "DV bilinear downsample");
                graph.AddBlitPass(new RenderGraphUtils.BlitMaterialParameters(low, output, Display.EffectMaterial, 0),
                    passName: "DV soft blur, chroma and color bands");
                resources.cameraColor = output;
            }
        }

        CamcorderPass pass;
        public override void Create() => pass = new CamcorderPass();

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData data)
        {
            var camera = data.cameraData.camera;
            if (!EffectShader || data.cameraData.cameraType != CameraType.Game ||
                !camera.TryGetComponent<DVCamcorderDisplay>(out var display) ||
                !display.isActiveAndEnabled || !display.EffectMaterial ||
                camera.targetTexture && !display.RenderToTexture) return;
            pass.Display = display;
            renderer.EnqueuePass(pass);
        }
    }
}
