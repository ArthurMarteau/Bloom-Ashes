using UnityEngine;

// Point d'entrée principal de la session de jeu
// Initialise les systèmes dans le bon ordre : équipes, ressources, etc.
// Sera étendu pour gérer les conditions de victoire, le spawn des joueurs, le netcode
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("Configuration")]
    // Active la sélection et les ordres sur toutes les équipes (usage dev/test)
    [SerializeField] private bool devModeAllTeams = true;

    // Expose devModeAllTeams pour SelectionManager et OrderManager
    public bool DevModeAllTeams => devModeAllTeams;

    [Header("Debug")]
    [SerializeField] private GameLoggerSettings loggerSettings;
    [SerializeField] private bool debugGame = true;

    // Initialise le singleton et les systèmes de jeu
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        InitializeGameLogger();
    }

    // Applique la configuration de logs assignée dans l'Inspector si elle existe.
    private void InitializeGameLogger()
    {
        if (loggerSettings != null)
        {
            loggerSettings.Apply();
            return;
        }

        GameLogger.EnabledCategories = GameLogger.Category.None;
    }

    // Point d'entrée de la session — les équipes sont déjà enregistrées par PlayerManager
    private void Start()
    {
        if (debugGame)
            GameLogger.Log(GameLogger.Category.Selection, "🎮 GameManager: Session démarrée", this);
    }
}
