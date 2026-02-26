using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.RenderGraphModule;

namespace PhotoMode
{
    public class BlitRenderPass : ScriptableRenderPass
    {
        public Material blitMaterial = null;
        private RTHandle m_TemporaryColorTexture;
        private string m_ProfilerTag;

        // Data class to pass references into the Render Graph lambda
        private class PassData
        {
            public Material material;
            public TextureHandle source;
            public TextureHandle tempTarget;
        }

        public BlitRenderPass(RenderPassEvent renderPassEvent, Material blitMat, string tag)
        {
            this.renderPassEvent = renderPassEvent;
            blitMaterial = blitMat;
            m_ProfilerTag = tag;

            // Pre-allocate the handle name. The actual texture size is managed by ReAllocateIfNeeded.
            m_TemporaryColorTexture = RTHandles.Alloc("_TemporaryColorTexture", name: "_TemporaryColorTexture");
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            if (blitMaterial == null) return;

            // 1. Get URP-specific frame data (Camera textures, etc.)
            UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
            UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();

            // 2. Ensure our temporary texture matches the current camera descriptor
            RenderTextureDescriptor desc = cameraData.cameraTargetDescriptor;
            desc.depthBufferBits = 0; // We only need color for a blit
            RenderingUtils.ReAllocateIfNeeded(ref m_TemporaryColorTexture, desc);

            // 3. Create the Render Graph Pass
            using (var builder = renderGraph.AddRasterRenderPass<PassData>(m_ProfilerTag, out var passData))
            {
                // Convert RTHandles to Render Graph TextureHandles
                TextureHandle cameraColor = resourceData.activeColorTexture;
                TextureHandle tempTexture = renderGraph.ImportTexture(m_TemporaryColorTexture);

                passData.material = blitMaterial;
                passData.source = cameraColor;
                passData.tempTarget = tempTexture;

                // Define Inputs and Outputs
                builder.UseTexture(passData.source);
                builder.SetRenderAttachment(passData.tempTarget, 0);

                // 4. The Execution Logic
                builder.SetRenderFunc((PassData data, RasterGraphContext context) =>
                {
                    // Pass 1: Camera -> Temp (with Material)
                    Blitter.BlitTexture(context.cmd, data.source, new Vector4(1, 1, 0, 0), data.material, 0);
                    
                    // Note: If you need to blit BACK to the camera, you typically 
                    // need a second pass or a sub-pass. However, most PhotoMode 
                    // effects can be done in one pass by setting the Camera as the Attachment.
                });
            }

            // 5. Final Pass: Blit Temp back to Camera
            using (var builder = renderGraph.AddRasterRenderPass<PassData>(m_ProfilerTag + "_Final", out var passData))
            {
                passData.source = renderGraph.ImportTexture(m_TemporaryColorTexture);
                passData.tempTarget = resourceData.activeColorTexture;

                builder.UseTexture(passData.source);
                builder.SetRenderAttachment(passData.tempTarget, 0);

                builder.SetRenderFunc((PassData data, RasterGraphContext context) =>
                {
                    // Pass 2: Temp -> Camera (standard copy)
                    Blitter.BlitTexture(context.cmd, data.source, new Vector4(1, 1, 0, 0), 0, false);
                });
            }
        }

        // Keep this empty for Unity 6; it's only used if Compatibility Mode is enabled.
        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData) { }

        // Proper Dispose pattern for RTHandles
        public void Dispose()
        {
            m_TemporaryColorTexture?.Release();
        }
    }
}
