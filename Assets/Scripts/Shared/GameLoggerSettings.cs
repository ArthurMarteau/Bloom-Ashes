using UnityEngine;

[CreateAssetMenu(menuName = "Bloom and Ashes/Logger Settings")]
public class GameLoggerSettings : ScriptableObject
{
    [SerializeField] private GameLogger.Category enabledCategories = GameLogger.Category.None;

    // Applique la configuration de logs centralisée au démarrage.
    public void Apply()
    {
        GameLogger.EnabledCategories = enabledCategories;
    }
}
