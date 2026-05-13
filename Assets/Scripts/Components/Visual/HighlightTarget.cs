using UnityEngine;

// Attaché sur chaque prefab d'unité
// Gère un SpriteRenderer enfant dédié à l'outline pixel art
// L'outline est activée/désactivée par OutlineController via Enable/Disable
public class HighlightTarget : MonoBehaviour
{
    // Matériau outline (shader Custom/SpriteOutline) — assigné dans l'Inspector ou auto-créé
    [SerializeField] private Material outlineMaterial;
    [SerializeField, Range(1f, 8f)] private float outlineSize = 2f;

    // SpriteRenderer principal de cette unité
    private SpriteRenderer mainRenderer;

    // SpriteRenderer enfant dédié à l'outline (créé dynamiquement si absent)
    private SpriteRenderer outlineRenderer;

    // Résout les références et crée le SpriteRenderer outline au démarrage
    private void Awake()
    {
        mainRenderer = GetComponent<SpriteRenderer>();

        if (mainRenderer == null)
        {
            Debug.LogError($"HighlightTarget sur {name} : aucun SpriteRenderer trouvé sur ce GameObject !");
            return;
        }

        SetupOutlineRenderer();
    }

    // Crée un SpriteRenderer enfant pour l'outline, en dessous du sprite principal
    private void SetupOutlineRenderer()
    {
        // Cherche un enfant existant nommé "_Outline" (évite les doublons à l'édition)
        Transform existing = transform.Find("_Outline");
        if (existing != null)
        {
            outlineRenderer = existing.GetComponent<SpriteRenderer>();
        }
        else
        {
            GameObject outlineGO = new GameObject("_Outline");
            outlineGO.transform.SetParent(transform, false);
            outlineGO.transform.localPosition = Vector3.zero;
            outlineGO.transform.localScale    = Vector3.one;
            outlineRenderer = outlineGO.AddComponent<SpriteRenderer>();
        }

        // Copie le sprite principal
        outlineRenderer.sprite       = mainRenderer.sprite;
        outlineRenderer.sortingLayerID = mainRenderer.sortingLayerID;
        outlineRenderer.sortingOrder   = mainRenderer.sortingOrder - 1; // derrière le sprite

        // Crée un matériau outline instancié par unité (nécessaire pour MaterialPropertyBlock + _OutlineSize)
        Shader outlineShader = Shader.Find("Custom/SpriteOutline");
        if (outlineShader != null)
        {
            // Instance propre à cette unité — évite les conflits entre unités
            outlineRenderer.material = new Material(outlineShader);
        }
        else
        {
            Debug.LogError("HighlightTarget : Shader 'Custom/SpriteOutline' introuvable !");
        }

        outlineRenderer.enabled = false; // désactivé par défaut
    }

    // Synchronise le sprite et le mirroring de l'outline avec le renderer principal
    // Ne met à jour que si les valeurs ont changé (avoidé les rebatch inutiles + interférence flipX)
    private void LateUpdate()
    {
        if (outlineRenderer == null || mainRenderer == null) return;

        if (outlineRenderer.sprite != mainRenderer.sprite)
            outlineRenderer.sprite = mainRenderer.sprite;

        if (outlineRenderer.flipX != mainRenderer.flipX)
            outlineRenderer.flipX = mainRenderer.flipX;

        if (outlineRenderer.flipY != mainRenderer.flipY)
            outlineRenderer.flipY = mainRenderer.flipY;
    }

    // Active l'outline avec la couleur HP correspondante
    public void ShowOutline(Color color)
    {
        if (outlineRenderer == null) return;
        outlineRenderer.enabled = true;

        // Applique directement sur le matériau instancié (MaterialPropertyBlock ne supporte pas _TexelSize)
        Material mat = outlineRenderer.material;
        mat.SetColor("_OutlineColor", color);
        mat.SetFloat("_OutlineSize",  outlineSize);

        // Fournit manuellement la taille du texel car Unity ne l'injecte pas toujours via le renderer
        if (mainRenderer.sprite != null)
        {
            Texture2D tex = mainRenderer.sprite.texture;
            mat.SetVector("_MainTex_TexelSize", new Vector4(
                1f / tex.width,
                1f / tex.height,
                tex.width,
                tex.height
            ));
        }
    }

    // Désactive l'outline
    public void HideOutline()
    {
        if (outlineRenderer != null)
            outlineRenderer.enabled = false;
    }
}
