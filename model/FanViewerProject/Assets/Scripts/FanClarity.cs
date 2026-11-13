using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Rendering.Universal;

public sealed class FanClarity : ScriptableRendererFeature
{
    public Shader shader;
    Material material;
    ClarityPass pass;
    public static int renderWidth, renderHeight;
    public static bool shaderSupported;

    sealed class ClarityPass : ScriptableRenderPass
    {
        public Material material;

        public override void RecordRenderGraph(RenderGraph graph, ContextContainer context)
        {
            var resources = context.Get<UniversalResourceData>();
            if (resources.isActiveTargetBackBuffer || !material) return;
            var source = resources.activeColorTexture;
            var desc = source.GetDescriptor(graph);
            renderWidth = desc.width;
            renderHeight = desc.height;
            desc.name = "Fan clarity";
            desc.msaaSamples = MSAASamples.None;
            desc.depthBufferBits = DepthBits.None;
            var target = graph.CreateTexture(desc);
            graph.AddBlitPass(new RenderGraphUtils.BlitMaterialParameters(
                source, target, material, 0), "Fan detail contrast");
            resources.cameraColor = target;
        }
    }

    public override void Create()
    {
        CoreUtils.Destroy(material);
        if (shader) material = CoreUtils.CreateEngineMaterial(shader);
        shaderSupported = shader && shader.isSupported;
        pass = new ClarityPass
        {
            material = material,
            renderPassEvent = RenderPassEvent.AfterRenderingPostProcessing
        };
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData data)
    {
        // Keep the offscreen component-selection mask outside the clarity pass.
        if (material && data.cameraData.cameraType == CameraType.Game &&
            !data.cameraData.camera.targetTexture)
            renderer.EnqueuePass(pass);
    }

    protected override void Dispose(bool disposing) { CoreUtils.Destroy(material); }
}
