using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace PCShopSell.Game.Runtime
{
    // Opaque world only: interactive canvas text and controls keep their native resolution.
    public sealed class PixelTownRendererFeature : ScriptableRendererFeature
    {
        private Material material;
        private PixelPass pass;
        public override void Create()
        {
            pass?.Dispose();CoreUtils.Destroy(material);
            var shader=Resources.Load<Shader>("Shaders/PixelTown");
            if(shader==null)return;
            material=CoreUtils.CreateEngineMaterial(shader);
            pass=new PixelPass(material){renderPassEvent=RenderPassEvent.BeforeRenderingTransparents};
        }
        public override void AddRenderPasses(ScriptableRenderer renderer,ref RenderingData data)
        {
            if(pass==null||data.cameraData.isPreviewCamera||data.cameraData.renderType==CameraRenderType.Overlay)return;
            renderer.EnqueuePass(pass);
        }
        protected override void Dispose(bool disposing){pass?.Dispose();CoreUtils.Destroy(material);}
        private sealed class PixelPass : ScriptableRenderPass
        {
            private readonly Material material;
            private RTHandle copy;
            public PixelPass(Material material){this.material=material;}
            public override void OnCameraSetup(CommandBuffer cmd,ref RenderingData data)
            {
                var desc=data.cameraData.cameraTargetDescriptor;desc.depthBufferBits=0;desc.msaaSamples=1;
                RenderingUtils.ReAllocateIfNeeded(ref copy,desc,FilterMode.Point,TextureWrapMode.Clamp,name:"Pixel town source");
                float scale=Mathf.Max(1,Mathf.Round(desc.height/480f));
                material.SetVector("_PixelGrid",new Vector4(desc.width/scale,desc.height/scale,scale,0));
            }
            public override void Execute(ScriptableRenderContext context,ref RenderingData data)
            {
                var cmd=CommandBufferPool.Get("Pixel town / palette and ink edges");
                var source=data.cameraData.renderer.cameraColorTargetHandle;
                Blitter.BlitCameraTexture(cmd,source,copy);
                Blitter.BlitCameraTexture(cmd,copy,source,material,0);
                context.ExecuteCommandBuffer(cmd);CommandBufferPool.Release(cmd);
            }
            public void Dispose(){copy?.Release();copy=null;}
        }
    }
}
