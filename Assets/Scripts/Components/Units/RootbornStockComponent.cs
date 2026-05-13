using UnityEngine;
using UnityEngine.Events;

// Wrapper Unity autour de WatersapCollectionSystem pour le prefab Rootborn
// Gère la collecte passive, le stockage (0-10), l'auto-transfer et le manual trigger
[RequireComponent(typeof(TeamComponent))]
public class RootbornStockComponent : MonoBehaviour
{
    [Header("Configuration")]
    [Tooltip("Stock maximum de Watersap dans le Rootborn")]
    [SerializeField] private int maxStock = 10;

    [Tooltip("Intervalle de collecte en secondes")]
    [SerializeField] private float collectionInterval = 5f;

    [Header("Events")]
    [Tooltip("Déclenché quand le stock change (collecte ou transfert). Argument = stock courant.")]
    public UnityEvent<int> OnStockChanged = new UnityEvent<int>();

    [Tooltip("Déclenché quand un transfert au pool joueur a lieu (auto ou manuel). Argument = quantité transférée.")]
    public UnityEvent<int> OnTransferTriggered = new UnityEvent<int>();

    private WatersapCollectionSystem collectionSystem;
    private MovementController movementController;
    private TeamComponent teamComponent;

    public int CurrentStock => collectionSystem?.CurrentStock ?? 0;
    public int MaxStock => maxStock;

    // Initialise le système et s'abonne à l'event de démarrage de mouvement
    private void Awake()
    {
        collectionSystem = new WatersapCollectionSystem(maxStock, collectionInterval);
        teamComponent = GetComponent<TeamComponent>();
        movementController = GetComponent<MovementController>();

        if (movementController != null)
        {
            movementController.OnMovementStarted.AddListener(HandleMovementStarted);
        }
    }

    // Désabonne les events à la destruction du GameObject
    private void OnDestroy()
    {
        if (movementController != null)
        {
            movementController.OnMovementStarted.RemoveListener(HandleMovementStarted);
        }
    }

    // Tick uniquement si le Rootborn est idle (pas en mouvement)
    private void Update()
    {
        if (collectionSystem == null) return;

        if (movementController != null && movementController.IsMoving) return;

        bool collected = collectionSystem.Tick(Time.deltaTime);
        if (collected)
        {
            OnStockChanged.Invoke(collectionSystem.CurrentStock);

            // Auto-transfer quand le stock atteint le maximum
            if (collectionSystem.IsFull())
            {
                TriggerTransferViaAbilityManager();
            }
        }
    }

    // Reset le timer quand le Rootborn reçoit un ordre de mouvement
    private void HandleMovementStarted()
    {
        collectionSystem.ResetTimer();
        Debug.Log($"💧 RootbornStockComponent {gameObject.name} : timer reset (ordre reçu)");
    }

    // Tentative de transfert — appelée par WatersapTransferExecutor (auto et manuel)
    // Retourne false si stock = 0
    public bool TryTransfer()
    {
        if (collectionSystem == null) return false;
        if (!collectionSystem.CanTransfer())
        {
            Debug.Log($"💧 RootbornStockComponent {gameObject.name} : transfert rejeté (stock={collectionSystem.CurrentStock})");
            return false;
        }

        ExecuteTransfer();
        return true;
    }

    // Exécute le transfert vers le pool joueur (auto ou manuel)
    // Le timer N'EST PAS reset : la prochaine collecte continue normalement après le transfert
    private void ExecuteTransfer()
    {
        int amount = collectionSystem.TransferStock();
        if (amount <= 0) return;

        if (WatersapManager.Instance != null && teamComponent != null)
        {
            WatersapManager.Instance.AddWatersap(teamComponent.GetTeamID(), amount);
        }

        OnStockChanged.Invoke(collectionSystem.CurrentStock);
        OnTransferTriggered.Invoke(amount);

        Debug.Log($"💧 RootbornStockComponent {gameObject.name} : {amount} Watersap transférés");
    }

    // Route l'auto-transfer via AbilityManager pour déclencher les VFX de l'ability
    private AbilityData GetTransferAbilityData()
    {
        return GetComponent<UnitStatsComponent>()?.GetBaseStats()?.abilityData;
    }

    // Fallback sur ExecuteTransfer() si le SO est mal configuré ou AbilityManager est absent
    private void TriggerTransferViaAbilityManager()
    {
        AbilityData data = GetTransferAbilityData();
        if (data != null && AbilityManager.Instance != null)
            AbilityManager.Instance.TryExecuteAbility(gameObject, data);
        else
            ExecuteTransfer();
    }

    // Vérifie si le manual trigger est disponible — utilisé par l'UI pour griser le bouton
    public bool CanManualTrigger()
    {
        return collectionSystem != null && collectionSystem.CanManualTrigger();
    }

    // --- DEBUG ---

    // Simule une collecte d'1 Watersap (incrémente stock + invoke event + auto-transfer si plein)
    [ContextMenu("Debug: Add 1 Watersap")]
    private void DebugAddOneWatersap()
    {
        if (collectionSystem == null) return;
        collectionSystem.SetCurrentStockForDebug(collectionSystem.CurrentStock + 1);
        OnStockChanged.Invoke(collectionSystem.CurrentStock);
        if (collectionSystem.IsFull()) ExecuteTransfer();
    }

    // Fixe le stock à 9 — utile pour tester l'auto-transfer au prochain tick
    [ContextMenu("Debug: Set Stock to 9")]
    private void DebugSetStockTo9()
    {
        if (collectionSystem == null) return;
        collectionSystem.SetCurrentStockForDebug(9);
        OnStockChanged.Invoke(collectionSystem.CurrentStock);
    }

    // Force un transfert immédiat quel que soit le stock courant — pour debug uniquement
    [ContextMenu("Debug: Force Transfer")]
    public void ForceTransferForDebug()
    {
        ExecuteTransfer();
    }
}
