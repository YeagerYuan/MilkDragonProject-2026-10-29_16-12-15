using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace UnderAvatar
{
    public sealed class DetectionOutlineFeature : ScriptableRendererFeature
    {
        public Shader maskShader,edgeShader;
        Material mask,edge;
        OutlinePass pass;
        sealed class OutlinePass : ScriptableRenderPass
        {
            public DetectionOutline target;
            public Material mask,edge;
            sealed class MaskData {public DetectionOutline target;public Material material;}
            sealed class EdgeData {public TextureHandle mask;public Material material;}
            public OutlinePass() {renderPassEvent=RenderPassEvent.AfterRenderingPostProcessing+1;requiresIntermediateTexture=true;}
            public override void RecordRenderGraph(RenderGraph graph,ContextContainer frame)
            {
                var resources=frame.Get<UniversalResourceData>();
                if(resources.isActiveTargetBackBuffer || !target || !target.Visible) return;
                var descriptor=graph.GetTextureDesc(resources.activeColorTexture);
                descriptor.name="Laughter target silhouette";descriptor.colorFormat=GraphicsFormat.R8G8B8A8_UNorm;
                descriptor.clearBuffer=true;descriptor.clearColor=Color.clear;descriptor.depthBufferBits=DepthBits.None;descriptor.msaaSamples=MSAASamples.None;
                var silhouette=graph.CreateTexture(descriptor);
                using(var builder=graph.AddRasterRenderPass<MaskData>("Laughter silhouette through walls",out var data))
                {
                    data.target=target;data.material=mask;builder.SetRenderAttachment(silhouette,0);
                    builder.SetRenderFunc((MaskData d,RasterGraphContext ctx)=>
                    {
                        foreach(var part in d.target.Parts)
                            if(part.renderer && part.Visible && part.renderer.gameObject.activeInHierarchy)
                            {
                                var skin=part.renderer as SkinnedMeshRenderer;
                                int count=skin && skin.sharedMesh?skin.sharedMesh.subMeshCount:part.mesh?part.mesh.subMeshCount:0;
                                // Use the renderer's own skinned pose and transform exactly once.
                                for(int i=0;i<count;i++) ctx.cmd.DrawRenderer(part.renderer,d.material,i,0);
                            }
                    });
                }
                using(var builder=graph.AddRasterRenderPass<EdgeData>("Red detection border",out var data))
                {
                    data.mask=silhouette;data.material=edge;builder.UseTexture(silhouette);
                    builder.SetRenderAttachment(resources.activeColorTexture,0,AccessFlags.ReadWrite);
                    builder.SetRenderFunc((EdgeData d,RasterGraphContext ctx)=>Blitter.BlitTexture(ctx.cmd,d.mask,new Vector4(1,1,0,0),d.material,0));
                }
            }
        }
        public override void Create()
        {
            CoreUtils.Destroy(mask);CoreUtils.Destroy(edge);
            if(maskShader) mask=CoreUtils.CreateEngineMaterial(maskShader);
            if(edgeShader) edge=CoreUtils.CreateEngineMaterial(edgeShader);
            pass=new OutlinePass{mask=mask,edge=edge};
        }
        public override void AddRenderPasses(ScriptableRenderer renderer,ref RenderingData data)
        {
            if(!mask || !edge || data.cameraData.cameraType!=CameraType.Game) return;
            if(!data.cameraData.camera.TryGetComponent<DetectionOutline>(out var outline) || !outline.Visible) return;
            pass.target=outline;renderer.EnqueuePass(pass);
        }
        protected override void Dispose(bool disposing) {CoreUtils.Destroy(mask);CoreUtils.Destroy(edge);}
    }
}
