using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering;

namespace PhotoMode
{
    public class Blit : ScriptableRendererFeature
    {
        public Material blitMaterial = null;
        private BlitRenderPass blitRenderPass;

        public override void Create()
        {
            // We pass the settings to the constructor. 
            // The pass now handles its own internal temporary textures.
            blitRenderPass = new BlitRenderPass(RenderPassEvent.AfterRenderingPostProcessing, blitMaterial, name);
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (blitMaterial == null) {
                return;
            }

            // In Unity 6, we just enqueue. 
            // The RecordRenderGraph method inside the pass will find the textures it needs.
            renderer.EnqueuePass(blitRenderPass);
        }

        protected override void Dispose(bool disposing)
        {
            // Call the Dispose method we added to the BlitRenderPass 
            // to clean up its internal _TemporaryColorTexture.
            blitRenderPass?.Dispose();
        }
    }
}