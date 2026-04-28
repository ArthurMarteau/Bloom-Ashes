using UnityEngine;

// Singleton MonoBehaviour qui orchestre les VFX liés aux événements de spawn/remplacement d'entités
// Écoute EntitySpawnManager.OnEntityReplaced et OnEntitySpawned pour déclencher les effets visuels
public class VFXManager : MonoBehaviour
{
    public static VFXManager Instance { get; private set; }

    [Header("VFX Configurations")]
    [Tooltip("Données VFX jouées lors de l'éclosion d'un Bud vers une unité finale")]
    [SerializeField] private VFXData budHatchVFX;

    [Tooltip("Données VFX jouées lors du spawn d'un Rootborn par un Nexus")]
    [SerializeField] private VFXData rootbornSpawnVFX;

    // Initialisation du singleton
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    // Abonnement aux events de spawn et remplacement d'entités de l'EntitySpawnManager
    private void Start()
    {
        if (EntitySpawnManager.Instance == null)
        {
            Debug.LogWarning("✨ VFXManager : EntitySpawnManager introuvable — les VFX d'éclosion ne seront pas joués");
            return;
        }

        EntitySpawnManager.Instance.OnEntityReplaced.AddListener(HandleEntityReplaced);
        Debug.Log("✨ VFXManager : abonné à EntitySpawnManager.OnEntityReplaced");

        EntitySpawnManager.Instance.OnEntitySpawned.AddListener(HandleEntitySpawned);
        Debug.Log("✨ VFXManager : abonné à EntitySpawnManager.OnEntitySpawned");
    }

    // Désabonnement propre pour éviter les références pendantes
    private void OnDestroy()
    {
        if (EntitySpawnManager.Instance != null)
        {
            EntitySpawnManager.Instance.OnEntityReplaced.RemoveListener(HandleEntityReplaced);
            EntitySpawnManager.Instance.OnEntitySpawned.RemoveListener(HandleEntitySpawned);
        }
    }

    // Réagit au remplacement d'entité : déclenche le VFX selon le cas (éclosion ou annulation)
    private void HandleEntityReplaced(GameObject oldEntity, GameObject newEntity)
    {
        if (oldEntity == null || newEntity == null) return;

        // Cas Rootborn → Bud : abonnement à l'event de Hatching pour déclencher le flash fade-in
        BudComponent newBud = newEntity.GetComponent<BudComponent>();
        if (newBud != null)
        {
            newBud.OnHatchingStarted.AddListener(HandleHatchingStarted);
            return;
        }

        // Vérifie que l'ancienne entité est bien un Bud
        bool oldEntityIsBud = oldEntity.GetComponent<BudComponent>() != null;
        if (!oldEntityIsBud) return;

        // Cas annulation Bud → Rootborn : applique le même VFX que le spawn Nexus → Rootborn
        bool newEntityIsRootborn = newEntity.GetComponent<RootbornProductionController>() != null;
        if (newEntityIsRootborn)
        {
            PlayRootbornSpawnVFX(newEntity);
            return;
        }

        Debug.Log($"✨ VFXManager : éclosion Bud→Unité détectée ({newEntity.name}) — déclenchement VFX");

        if (budHatchVFX == null) return;

        // Particules + Light 2D + Son
        VFXPlayer.PlayAt(budHatchVFX, newEntity.transform.position);

        // Flash blanc sur le sprite de l'unité spawnée
        if (budHatchVFX.useSpriteFlash)
            ApplySpawnFlash(newEntity, budHatchVFX);

        // Effet scale up sur l'unité spawnée (synchro avec le flash fade-out)
        if (budHatchVFX.useScaleUp)
            ApplyScaleUp(newEntity, budHatchVFX);
    }

    // Réagit au spawn d'une entité sans remplacement
    // Cas géré : Nexus spawn un Rootborn → délègue à PlayRootbornSpawnVFX
    private void HandleEntitySpawned(GameObject spawner, GameObject spawnedEntity)
    {
        if (spawner == null || spawnedEntity == null) return;

        // Vérifie que le spawner est bien un Nexus (présence du NexusController)
        if (spawner.GetComponent<NexusController>() == null) return;

        PlayRootbornSpawnVFX(spawnedEntity);
    }

    // Joue le VFX de spawn Rootborn (utilisé pour Nexus → Rootborn ET annulation Bud → Rootborn)
    // Applique : particules + Light 2D + flash blanc fade-out + scale up
    private void PlayRootbornSpawnVFX(GameObject rootbornEntity)
    {
        if (rootbornEntity == null) return;

        if (rootbornSpawnVFX == null)
        {
            Debug.LogWarning("✨ VFXManager : rootbornSpawnVFX non assigné — VFX de spawn Rootborn ignoré");
            return;
        }

        Debug.Log($"✨ VFXManager : VFX spawn Rootborn déclenché sur {rootbornEntity.name}");

        // Particules + Light 2D + Son
        VFXPlayer.PlayAt(rootbornSpawnVFX, rootbornEntity.transform.position);

        // Flash blanc sur le sprite
        if (rootbornSpawnVFX.useSpriteFlash)
            ApplySpawnFlash(rootbornEntity, rootbornSpawnVFX);

        // Effet scale up
        if (rootbornSpawnVFX.useScaleUp)
            ApplyScaleUp(rootbornEntity, rootbornSpawnVFX);
    }

    // Applique un flash blanc fade-out sur le sprite de l'entité
    // Le SpriteFlashController est ajouté si absent
    private void ApplySpawnFlash(GameObject entity, VFXData data)
    {
        SpriteFlashController flashController = entity.GetComponent<SpriteFlashController>();
        if (flashController == null)
            flashController = entity.AddComponent<SpriteFlashController>();

        flashController.SetFlashAmount(1f);
        flashController.FadeOut(data.flashFadeDuration, data.flashFadeCurve);

        Debug.Log($"✨ VFXManager : flash appliqué sur {entity.name} — FadeOut {data.flashFadeDuration}s");
    }

    // Applique un effet scale up linéaire sur l'entité
    // Le ScaleUpController est ajouté si absent
    private void ApplyScaleUp(GameObject entity, VFXData data)
    {
        ScaleUpController scaleController = entity.GetComponent<ScaleUpController>();
        if (scaleController == null)
            scaleController = entity.AddComponent<ScaleUpController>();

        float scaleDuration = data.scaleUpUseFlashDuration
            ? data.flashFadeDuration
            : data.scaleUpCustomDuration;

        scaleController.StartScaleUp(
            data.scaleUpStartScale,
            data.scaleUpEndScale,
            scaleDuration
        );

        Debug.Log($"✨ VFXManager : scale up sur {entity.name} ({data.scaleUpStartScale} → {data.scaleUpEndScale} sur {scaleDuration}s)");
    }

    // Déclenche le flash fade-in sur le Bud au début de sa phase Hatching
    // Saturation blanche progressive de 0 → 1 sur la durée du Hatching (ease-in)
    private void HandleHatchingStarted(BudComponent bud)
    {
        if (bud == null) return;
        if (budHatchVFX == null || !budHatchVFX.useSpriteFlash) return;

        SpriteFlashController flashController = bud.GetComponent<SpriteFlashController>();
        if (flashController == null)
            flashController = bud.gameObject.AddComponent<SpriteFlashController>();

        // Reset à 0 avant le fade-in (sécurité)
        flashController.SetFlashAmount(0f);

        // Fade-in sur la durée du Hatching — utilise la même courbe que le fade-out (inversion implicite dans FlashCurveSystem.EvaluateFadeIn)
        flashController.FadeIn(bud.HatchingDuration, budHatchVFX.flashFadeCurve);

        Debug.Log($"✨ VFXManager : flash Hatching déclenché sur {bud.name} — FadeIn {bud.HatchingDuration}s");
    }

    // Méthode publique pour jouer un VFX arbitraire depuis d'autres systèmes
    public void PlayVFX(VFXData data, Vector3 position, Transform attach = null)
    {
        if (data == null)
        {
            Debug.LogWarning("✨ VFXManager.PlayVFX : VFXData null");
            return;
        }

        VFXPlayer.PlayAt(data, position, attach);
    }
}
