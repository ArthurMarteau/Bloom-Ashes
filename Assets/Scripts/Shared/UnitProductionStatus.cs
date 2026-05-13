// Statut d'une unité dans l'interface de production d'un bâtiment de faction
public enum UnitProductionStatus
{
    Available,           // Produisible immédiatement
    NotEnoughWatersap,   // Watersap insuffisant
    TierLocked           // Tier du bâtiment trop bas
}
