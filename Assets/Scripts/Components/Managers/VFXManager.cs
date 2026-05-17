using UnityEngine;

// Singleton MonoBehaviour qui orchestre les VFX liés aux événements de spawn/remplacement d'entités
// Écoute EntitySpawnManager.OnEntityReplaced et OnEntitySpawned pour les VFX de spawn
// Écoute AbilityManager.OnAbilityExecuted pour les VFX d'exécution d'abilities
public class VFXManager : MonoBehaviour
{
    public static VFXManager Instance { get; private set; }

    [Header("VFX Configurations")]
    [Tooltip("Données VFX jouées lors de l'éclosion d'un Bud vers une unité finale")]
    [SerializeField] private VFXData budHatchVFX;

    [Tooltip("Données VFX jouées lors du spawn d'un Rootborn par un Nexus")]
    [SerializeField] private VFXData rootbornSpawnVFX;

    [Tooltip("VFX joué sur l'unité de combat qui se transforme en Bud (revert vers Rootborn)")]
    [SerializeField] private VFXData unitRevertVFX;

    [Header("VFX Bâtiments")]
    [Tooltip("VFX joué à la destruction d'un bâtiment")]
    [SerializeField] private VFXData buildingDeathVFX;

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

    // Abonnement aux events de spawn, remplacement d'entités et exécution d'abilities
    private void Start()
    {
        if (EntitySpawnManager.Instance == null)
        {
            GameLogger.Warn(GameLogger.Category.UI, "✨ VFXManager : EntitySpawnManager introuvable — les VFX d'éclosion ne seront pas joués", this);
            return;
        }

        EntitySpawnManager.Instance.OnEntityReplaced.AddListener(HandleEntityReplaced);
        GameLogger.Log(GameLogger.Category.UI, "✨ VFXManager : abonné à EntitySpawnManager.OnEntityReplaced", this);

        EntitySpawnManager.Instance.OnEntitySpawned.AddListener(HandleEntitySpawned);
        GameLogger.Log(GameLogger.Category.UI, "✨ VFXManager : abonné à EntitySpawnManager.OnEntitySpawned", this);

        if (AbilityManager.Instance != null)
            AbilityManager.Instance.OnAbilityExecuted.AddListener(HandleAbilityExecuted);
    }

    // Désabonnement propre pour éviter les références pendantes
    private void OnDestroy()
    {
        if (EntitySpawnManager.Instance != null)
        {
            EntitySpawnManager.Instance.OnEntityReplaced.RemoveListener(HandleEntityReplaced);
            EntitySpawnManager.Instance.OnEntitySpawned.RemoveListener(HandleEntitySpawned);
        }

        if (AbilityManager.Instance != null)
            AbilityManager.Instance.OnAbilityExecuted.RemoveListener(HandleAbilityExecuted);
    }

    // Réagit au remplacement d'entité : déclenche le VFX selon le cas (éclosion ou annulation)
    private void HandleEntityReplaced(GameObject oldEntity, GameObject newEntity)
    {
        if (oldEntity == null || newEntity == null) return;

        // Unité combat → Bud : flash fade-out sur l'unité source avant destruction
        bool oldIsUnit = oldEntity.GetComponent<UnitStatsComponent>() != null
                      && oldEntity.GetComponent<BudComponent>() == null
                      && oldEntity.GetComponent<RootbornStockComponent>() == null;
        bool newIsBud = newEntity.GetComponent<BudComponent>() != null;

        if (oldIsUnit && newIsBud && unitRevertVFX != null)
        {
            VFXPlayer.PlayAt(unitRevertVFX, oldEntity.transform.position);
            if (unitRevertVFX.useSpriteFlash) ApplySpawnFlash(newEntity, unitRevertVFX);
            if (unitRevertVFX.useScaleUp) ApplyScaleUp(newEntity, unitRevertVFX);
        }

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
        bool newEntityIsRootborn = newEntity.GetComponent<RootbornStockComponent>() != null;
        if (newEntityIsRootborn)
        {
            PlayRootbornSpawnVFX(newEntity);
            return;
        }

        GameLogger.Log(GameLogger.Category.UI, $"✨ VFXManager : éclosion Bud→Unité détectée ({newEntity.name}) — déclenchement VFX", this);

        if (budHatchVFX == null) return;

        // Particules + Son
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
    // Applique : particules + son + flash blanc fade-out + scale up
    private void PlayRootbornSpawnVFX(GameObject rootbornEntity)
    {
        if (rootbornEntity == null) return;

        if (rootbornSpawnVFX == null)
        {
            GameLogger.Warn(GameLogger.Category.UI, "✨ VFXManager : rootbornSpawnVFX non assigné — VFX de spawn Rootborn ignoré", this);
            return;
        }

        GameLogger.Log(GameLogger.Category.UI, $"✨ VFXManager : VFX spawn Rootborn déclenché sur {rootbornEntity.name}", this);

        // Particules + Son
        VFXPlayer.PlayAt(rootbornSpawnVFX, rootbornEntity.transform.position);

        // Flash blanc sur le sprite
        if (rootbornSpawnVFX.useSpriteFlash)
            ApplySpawnFlash(rootbornEntity, rootbornSpawnVFX);

        // Effet scale up
        if (rootbornSpawnVFX.useScaleUp)
            ApplyScaleUp(rootbornEntity, rootbornSpawnVFX);
    }

    // Réagit à l'exécution d'une ability : joue le VFX configuré dans AbilityData.vfxOnExecute
    private void HandleAbilityExecuted(GameObject caster, AbilityData abilityData)
    {
        if (caster == null || abilityData == null || abilityData.vfxOnExecute == null) return;

        GameLogger.Log(GameLogger.Category.UI, $"✨ VFXManager : VFX ability '{abilityData.abilityName}' sur {caster.name}", this);
        VFXPlayer.PlayAt(abilityData.vfxOnExecute, caster.transform.position);

        if (abilityData.vfxOnExecute.useWaveFill)     ApplyWaveFill(caster, abilityData.vfxOnExecute);
        if (abilityData.vfxOnExecute.useSpriteFlash)  ApplySpawnFlash(caster, abilityData.vfxOnExecute);
        if (abilityData.vfxOnExecute.useScaleUp)      ApplyScaleUp(caster, abilityData.vfxOnExecute);
    }

    // Applique un flash blanc fade-out sur le sprite de l'entité
    // Le SpriteFlashController est ajouté si absent
    private void ApplySpawnFlash(GameObject entity, VFXData data)
    {
        SpriteFlashController flashController = entity.GetComponent<SpriteFlashController>();
        if (flashController == null)
            flashController = entity.AddComponent<SpriteFlashController>();

        flashController.SetFlashColor(data.flashColor);
        flashController.SetFlashAmount(1f);
        flashController.FadeOut(data.flashFadeDuration, data.flashFadeCurve);

        GameLogger.Log(GameLogger.Category.UI, $"✨ VFXManager : flash appliqué sur {entity.name} — FadeOut {data.flashFadeDuration}s", this);
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

        GameLogger.Log(GameLogger.Category.UI, $"✨ VFXManager : scale up sur {entity.name} ({data.scaleUpStartScale} → {data.scaleUpEndScale} sur {scaleDuration}s)", this);
    }

    // Applique l'effet de vague Wave Fill qui remonte le sprite du bas vers le haut
    // Le WaveFillController est ajouté dynamiquement si absent
    private void ApplyWaveFill(GameObject entity, VFXData data)
    {
        WaveFillController fillController = entity.GetComponent<WaveFillController>();
        if (fillController == null)
            fillController = entity.AddComponent<WaveFillController>();

        fillController.Configure(data.waveFillColor, data.waveFillEdgeWidth);
        fillController.PlayFillAnimation(
            data.waveFillDuration,
            data.waveFillHoldDuration,
            data.waveFillCurve
        );

        GameLogger.Log(GameLogger.Category.UI, $"✨ VFXManager : wave fill sur {entity.name} ({data.waveFillDuration}s + hold {data.waveFillHoldDuration}s)", this);
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

        GameLogger.Log(GameLogger.Category.UI, $"✨ VFXManager : flash Hatching déclenché sur {bud.name} — FadeIn {bud.HatchingDuration}s", this);
    }

    // Méthode publique pour jouer un VFX arbitraire depuis d'autres systèmes
    public void PlayUnitRevertStartVFX(GameObject unit)
    {
        if (unitRevertVFX == null || unit == null) return;

        if (unitRevertVFX.useSpriteFlash)
        {
            SpriteFlashController flash = unit.GetComponent<SpriteFlashController>()
                ?? unit.AddComponent<SpriteFlashController>();
            flash.SetFlashAmount(0f);
            flash.FadeIn(unitRevertVFX.flashFadeDuration, unitRevertVFX.flashFadeCurve);
        }
    }

    // Joue un VFXData à une position donnée depuis d'autres systèmes
    public void PlayVFX(VFXData data, Vector3 position, Transform attach = null)
    {
        if (data == null)
        {
            GameLogger.Warn(GameLogger.Category.UI, "✨ VFXManager.PlayVFX : VFXData null", this);
            return;
        }

        VFXPlayer.PlayAt(data, position, attach);
    }

    // Joue le VFX de destruction de bâtiment à la position donnée
    public void PlayBuildingDeathVFX(Vector3 position)
    {
        if (buildingDeathVFX == null) return;

        VFXPlayer.PlayAt(buildingDeathVFX, position);
    }
}
