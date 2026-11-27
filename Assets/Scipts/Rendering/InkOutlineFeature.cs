using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering;
using UnityEngine;

namespace Zvezda57.Rendering
{
    public sealed class InkOutlineFeature : ScriptableRendererFeature
    {
        [SerializeField]
        private Material material;
        private InkPass _pass;
        public void Configure(Material value)
        {
            material = value;
            Create();
        }

        public override void Create()
        {
            _pass = new InkPass(material);
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData data)
        {
            if (material == null || data.cameraData.cameraType == CameraType.Preview || data.cameraData.cameraType == CameraType.Reflection)
            {
                return;
            }

            renderer.EnqueuePass(_pass);
        }

        private sealed class InkPass : ScriptableRenderPass
        {
            private readonly Material _material;

            private sealed class PassData
            {
                public TextureHandle Source;
                public Material Material;
            }

            public InkPass(Material material)
            {
                _material = material;
                renderPassEvent = RenderPassEvent.BeforeRenderingTransparents;
                ConfigureInput(ScriptableRenderPassInput.Depth | ScriptableRenderPassInput.Normal);
                requiresIntermediateTexture = true;
            }

            public override void RecordRenderGraph(RenderGraph graph, ContextContainer frameData)
            {
                var resources = frameData.Get<UniversalResourceData>();
                if (resources.isActiveTargetBackBuffer)
                {
                    return;
                }

                var desc = graph.GetTextureDesc(resources.activeColorTexture);
                desc.name = "Office ink outlines";
                desc.clearBuffer = false;
                var destination = graph.CreateTexture(desc);
                using (var builder = graph.AddRasterRenderPass<PassData>("Office ink outlines", out var data))
                {
                    data.Source = resources.activeColorTexture;
                    data.Material = _material;
                    builder.UseTexture(data.Source);
                    if (resources.cameraDepthTexture.IsValid())
                    {
                        builder.UseTexture(resources.cameraDepthTexture);
                    }

                    if (resources.cameraNormalsTexture.IsValid())
                    {
                        builder.UseTexture(resources.cameraNormalsTexture);
                    }

                    builder.UseAllGlobalTextures(true);
                    builder.SetRenderAttachment(destination, 0);
                    builder.SetRenderFunc((PassData d, RasterGraphContext context) => Blitter.BlitTexture(context.cmd, d.Source, new Vector4(1, 1, 0, 0), d.Material, 0));
                }

                resources.cameraColor = destination;
            }
        }
    }
}
