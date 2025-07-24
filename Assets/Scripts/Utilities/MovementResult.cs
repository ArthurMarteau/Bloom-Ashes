namespace Utilities
{
    /// <summary>
    /// Énumération des résultats possibles d'un déplacement d'unité
    /// </summary>
    public enum MovementResult 
    {
        Success,        // Arrivé à destination finale
        PartialSuccess, // Progrès fait, peut continuer immédiatement  
        Blocked,        // Vraiment bloqué, besoin d'attendre un peu
        Failed          // Échec définitif, abandon du déplacement
    }
}