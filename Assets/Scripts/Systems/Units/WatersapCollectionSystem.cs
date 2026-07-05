// POCO pur — gère le timer de collecte passive et le stock interne de Watersap d'un Rootborn
// Testable sans Unity : aucune dépendance à MonoBehaviour, GameObject ou Time
public class WatersapCollectionSystem
{
    private readonly int maxStock;
    private readonly float collectionInterval;
    private float timer;
    private int elapsedTicks;
    private int currentStock;

    public int CurrentStock => currentStock;
    public int MaxStock => maxStock;
    public float Timer => timer;
    public float CollectionInterval => collectionInterval;

    // Initialise le système avec les paramètres de collecte
    public WatersapCollectionSystem(int maxStock = 10, float collectionInterval = 5f)
    {
        this.maxStock = maxStock;
        this.collectionInterval = collectionInterval;
        this.timer = 0f;
        this.currentStock = 0;
    }

    // Avance le timer de collecte — retourne true si un Watersap vient d'être collecté
    // Si le stock est plein, ne collecte pas (le component doit déclencher l'auto-transfer en amont)
    [System.Obsolete("Utiliser TickOnce pour la simulation lockstep.")]
    public bool Tick(float deltaTime)
    {
        if (currentStock >= maxStock) return false;

        timer += deltaTime;
        if (timer >= collectionInterval)
        {
            timer -= collectionInterval;
            currentStock++;
            return true;
        }
        return false;
    }

    // Avance le compteur logique et collecte une unité lorsque l'intervalle en ticks est atteint
    public bool TickOnce(int tickCount, int intervalTicks)
    {
        if (currentStock >= maxStock || tickCount <= 0)
            return false;

        elapsedTicks += tickCount;
        int safeIntervalTicks = System.Math.Max(1, intervalTicks);
        if (elapsedTicks < safeIntervalTicks)
            return false;

        elapsedTicks -= safeIntervalTicks;
        currentStock++;
        return true;
    }

    // Remet le timer à zéro — appelé quand le Rootborn reçoit un ordre (move, build, transform…)
    public void ResetTimer()
    {
        timer = 0f;
        elapsedTicks = 0;
    }

    // Vide le stock et retourne la quantité transférée — à ajouter au pool joueur
    public int TransferStock()
    {
        int transferred = currentStock;
        currentStock = 0;
        return transferred;
    }

    // Vérifie si un transfert est possible (logique métier)
    // Utilisé par auto-transfer ET manual transfer
    public bool CanTransfer()
    {
        return currentStock >= 1;
    }

    // Vérifie si le manual trigger UI est disponible
    // Stock 0 → rien à transférer | Stock 10 → auto-transfer imminent, bouton UI grisé
    public bool CanManualTrigger()
    {
        return currentStock >= 1 && currentStock < maxStock;
    }

    // Retourne true si le stock est plein (auto-transfer à déclencher par le component)
    public bool IsFull() => currentStock >= maxStock;

    // Pour debug uniquement — permet de forcer une valeur de stock depuis l'éditeur
    public void SetCurrentStockForDebug(int value)
    {
        currentStock = UnityEngine.Mathf.Clamp(value, 0, maxStock);
    }
}
