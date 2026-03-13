using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Experimental.Rendering;

// Render Feature URP Unity 6 — Outline par layer
// Détecte les objets sur le layer "Outline", génère leur silhouette en masque,
// puis applique une edge detection screen-space pour dessiner l'outline coloré.
//
// Pipeline d'un frame :
//   [Pass 1] Mask   — Rendu des objets Outline → silhouette blanche sur fond noir (maskHandle)
//   [Pass 2] Copy   — Copie du color buffer actif (colorCopyHandle) pour éviter le conflit read/write
//   [Pass 3] Composite — Edge detection sur le masque → composite outline sur le color buffer final
//
// ⚠️ Pré-requis Unity :
//   - Layer "Outline" existant dans Project Settings > Tags and Layers
//   - Shader "Custom/OutlineEffect" (Assets/Shaders/OutlineEffect.shader) présent dans le projet
//   - Ce Renderer Feature ajouté dans l'URP Renderer Asset (Add Renderer Feature)
//
// ⚠️ Note sur le masque (Pass 1) :
//   Le RendererList utilise un overrideMaterial qui remplace le matériau des sprites.
//   Il ne peut pas accéder à la texture d'origine du sprite. La silhouette correspond
//   donc au mesh du sprite. Pour un résultat optimal, préférez des sprites avec
//   "Mesh Type = Tight" (mesh calculé sur les contours alpha).
public class OutlineRendererFeature : ScriptableRendererFeature
{
    [Header("Filtre")]
    [Tooltip("Layer contenant les objets à outliniser (ex: layer 'Outline')")]
    public LayerMask outlineLayer;

    [Header("Apparence")]
    public Color outlineColor = Color.white;
    [Range(0.5f, 8f), Tooltip("Épaisseur de l'outline en texels")]
    public float outlineScale = 1f;
    [Range(0f, 1f), Tooltip("Seuil alpha pour détecter la bordure du masque")]
    public float normalThreshold = 0.4f;

    [Header("Shader (auto-résolu si vide)")]
    [SerializeField] private Shader outlineShader;

    private OutlineRenderPass renderPass;
    private Material silhouetteMaterial; // Pass 0 : rendu silhouette blanche
    private Material compositeMaterial;  // Pass 1 : edge detection + composite

    // Crée les matériaux et le RenderPass
    public override void Create()
    {
        if (outlineShader == null)
            outlineShader = Shader.Find("Custom/OutlineEffect");

        if (outlineShader == null)
        {
            Debug.LogError("OutlineRendererFeature: Shader 'Custom/OutlineEffect' introuvable !\n" +
                           "Vérifiez que Assets/Shaders/OutlineEffect.shader est présent dans le projet.");
            return;
        }

        // Deux instances distinctes pour ne pas mélanger les propriétés des deux passes
        silhouetteMaterial = CoreUtils.CreateEngineMaterial(outlineShader);
        compositeMaterial  = CoreUtils.CreateEngineMaterial(outlineShader);

        renderPass = new OutlineRenderPass(silhouetteMaterial, compositeMaterial);
        renderPass.renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing;
    }

    // Enfile le pass si la caméra de jeu est active (pas de preview caméra, pas de SceneView)
    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (renderPass == null || silhouetteMaterial == null || compositeMaterial == null) return;
        if (renderingData.cameraData.cameraType != CameraType.Game) return;

        renderPass.UpdateSettings(outlineLayer, outlineColor, outlineScale, normalThreshold);
        renderer.EnqueuePass(renderPass);
    }

    // Libère les matériaux créés dynamiquement
    protected override void Dispose(bool disposing)
    {
        CoreUtils.Destroy(silhouetteMaterial);
        CoreUtils.Destroy(compositeMaterial);
    }

    // =========================================================================
    // RENDER PASS — Logique RenderGraph (Unity 6, non-deprecated)
    // =========================================================================
    class OutlineRenderPass : ScriptableRenderPass
    {
        // Tags shader acceptés pour le RendererList (sprites URP 2D + unlit)
        private static readonly ShaderTagId[] ShaderTagIds =
        {
            new ShaderTagId("Universal2D"),
            new ShaderTagId("SRPDefaultUnlit"),
            new ShaderTagId("UniversalForward"),
        };

        private readonly Material silhouetteMaterial;
        private readonly Material compositeMaterial;

        private LayerMask outlineLayer;
        private Color     outlineColor;
        private float     outlineScale;
        private float     normalThreshold;

        // ---- Structs de données pour RenderGraph (value types) ----------------

        private class MaskPassData
        {
            public RendererListHandle rendererList;
        }

        private class CopyPassData
        {
            public TextureHandle source;
        }

        private class CompositePassData
        {
            public TextureHandle colorSource; // scène copiée
            public TextureHandle mask;        // silhouette
        }

        // -----------------------------------------------------------------------

        public OutlineRenderPass(Material silhouetteMat, Material compositeMat)
        {
            silhouetteMaterial = silhouetteMat;
            compositeMaterial  = compositeMat;
        }

        // Met à jour les paramètres depuis la Render Feature à chaque frame
        public void UpdateSettings(LayerMask layer, Color color, float scale, float threshold)
        {
            outlineLayer    = layer;
            outlineColor    = color;
            outlineScale    = scale;
            normalThreshold = threshold;
        }

        // Point d'entrée RenderGraph — remplace l'ancienne méthode Execute (dépréciée Unity 6)
        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            var resourceData = frameData.Get<UniversalResourceData>();
            var cameraData   = frameData.Get<UniversalCameraData>();
            var renderingData = frameData.Get<UniversalRenderingData>();

            // Résolution de la caméra (sans MSAA pour les textures intermédiaires)
            var camDesc = cameraData.cameraTargetDescriptor;
            int width   = camDesc.width;
            int height  = camDesc.height;

            // --- Création des textures temporaires ---

            // Masque : R8 suffisant (silhouette noir/blanc)
            TextureHandle maskHandle = renderGraph.CreateTexture(new TextureDesc(width, height)
            {
                colorFormat  = GraphicsFormat.R8_UNorm,
                clearBuffer  = true,
                clearColor   = Color.clear,
                msaaSamples  = MSAASamples.None,
                name         = "_OutlineMask"
            });

            // Copie couleur : même format que le color buffer actif
            TextureHandle colorCopyHandle = renderGraph.CreateTexture(new TextureDesc(width, height)
            {
                colorFormat = camDesc.graphicsFormat,
                clearBuffer = false,
                msaaSamples = MSAASamples.None,
                name        = "_OutlineColorCopy"
            });

            // === Pass 1 : Silhouette des objets Outline → maskHandle ===
            RecordMaskPass(renderGraph, maskHandle, renderingData, cameraData);

            // === Pass 2 : Copie du color buffer actif → colorCopyHandle ===
            RecordCopyPass(renderGraph, resourceData.activeColorTexture, colorCopyHandle);

            // === Pass 3 : Edge detection + composite → activeColorTexture ===
            RecordCompositePass(renderGraph, colorCopyHandle, maskHandle, resourceData.activeColorTexture);
        }

        // --- Pass 1 : Rendu silhouette blanche des objets sur le layer Outline ---
        private void RecordMaskPass(RenderGraph renderGraph, TextureHandle maskHandle,
            UniversalRenderingData renderingData, UniversalCameraData cameraData)
        {
            var sortingSettings = new SortingSettings(cameraData.camera)
            {
                criteria = SortingCriteria.CommonTransparent
            };

            // DrawingSettings : overrideMaterial = silhouetteMaterial (Pass 0 du shader)
            var drawingSettings = new DrawingSettings(ShaderTagIds[0], sortingSettings)
            {
                overrideMaterial          = silhouetteMaterial,
                overrideMaterialPassIndex = 0,
                enableDynamicBatching     = false
            };
            for (int i = 1; i < ShaderTagIds.Length; i++)
                drawingSettings.SetShaderPassName(i, ShaderTagIds[i]);

            // FilteringSettings : filtre sur le layer Outline uniquement
            var filteringSettings = new FilteringSettings(RenderQueueRange.all, outlineLayer);

            var listParams = new RendererListParams(renderingData.cullResults, drawingSettings, filteringSettings);
            RendererListHandle rendererListHandle = renderGraph.CreateRendererList(listParams);

            using (var builder = renderGraph.AddRasterRenderPass<MaskPassData>("Outline_MaskPass", out var passData))
            {
                passData.rendererList = rendererListHandle;
                builder.SetRenderAttachment(maskHandle, 0, AccessFlags.Write);
                builder.UseRendererList(rendererListHandle);
                builder.SetRenderFunc(static (MaskPassData data, RasterGraphContext context) =>
                {
                    context.cmd.DrawRendererList(data.rendererList);
                });
            }
        }

        // --- Pass 2 : Copie simple du color buffer actif (évite le conflit read/write en Pass 3) ---
        private void RecordCopyPass(RenderGraph renderGraph, TextureHandle source, TextureHandle dest)
        {
            using (var builder = renderGraph.AddRasterRenderPass<CopyPassData>("Outline_CopyPass", out var passData))
            {
                passData.source = source;
                builder.SetRenderAttachment(dest, 0, AccessFlags.Write);
                builder.UseTexture(source, AccessFlags.Read);
                builder.SetRenderFunc(static (CopyPassData data, RasterGraphContext context) =>
                {
                    // Blit passthrough (mip 0, bilinear off pour ne pas interpoler les pixels)
                    Blitter.BlitTexture(context.cmd, data.source, new Vector4(1f, 1f, 0f, 0f), 0f, false);
                });
            }
        }

        // --- Pass 3 : Edge detection sur le masque, composite de l'outline sur le color buffer ---
        private void RecordCompositePass(RenderGraph renderGraph,
            TextureHandle colorSource, TextureHandle mask, TextureHandle colorTarget)
        {
            // Capture des paramètres pour la lambda (value types → pas d'allocation heap)
            Color  capturedColor     = outlineColor;
            float  capturedScale     = outlineScale;
            float  capturedThreshold = normalThreshold;
            Material capturedMat     = compositeMaterial;

            using (var builder = renderGraph.AddRasterRenderPass<CompositePassData>("Outline_CompositePass", out var passData))
            {
                passData.colorSource = colorSource;
                passData.mask        = mask;

                builder.SetRenderAttachment(colorTarget, 0, AccessFlags.Write);
                builder.UseTexture(colorSource, AccessFlags.Read);
                builder.UseTexture(mask,        AccessFlags.Read);

                builder.SetRenderFunc((CompositePassData data, RasterGraphContext context) =>
                {
                    // Propriétés du composite shader (Pass 1)
                    capturedMat.SetColor("_OutlineColor",      capturedColor);
                    capturedMat.SetFloat("_OutlineSize",        capturedScale);
                    capturedMat.SetFloat("_NormalThreshold",    capturedThreshold);
                    capturedMat.SetTexture("_MaskTex",          data.mask);

                    // Blit : source = scène copiée, material pass 1 = composite outline
                    Blitter.BlitTexture(context.cmd, data.colorSource, new Vector4(1f, 1f, 0f, 0f), capturedMat, 1);
                });
            }
        }
    }
}
