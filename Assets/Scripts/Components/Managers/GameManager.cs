using UnityEngine;

// Point d'entrée principal de la session de jeu
// Initialise les systèmes dans le bon ordre : équipes, ressources, etc.
// Sera étendu pour gérer les conditions de victoire, le spawn des joueurs, le netcode
public enum MatchState
{
    Setup,
    Playing,
    Ended
}

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("Configuration")]
    // Active la sélection et les ordres sur toutes les équipes (usage dev/test)
    [SerializeField] private bool devModeAllTeams = true;

    // Expose devModeAllTeams pour SelectionManager et OrderManager
    public bool DevModeAllTeams => devModeAllTeams;
    public MatchState CurrentMatchState { get; private set; } = MatchState.Setup;
    public MatchConfig CurrentMatchConfig { get; private set; }

    [Header("Match")]
    public UnityEngine.Events.UnityEvent<MatchState> OnMatchStateChanged = new UnityEngine.Events.UnityEvent<MatchState>();
    public UnityEngine.Events.UnityEvent<int, System.Collections.Generic.IReadOnlyList<int>> OnMatchEnded = new UnityEngine.Events.UnityEvent<int, System.Collections.Generic.IReadOnlyList<int>>();

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

    // Termine le setup et démarre les systèmes déterministes du match.
    public void StartPlaying(MatchConfig config)
    {
        if (CurrentMatchState != MatchState.Setup || config == null)
            return;

        CurrentMatchConfig = config;
        VictoryManager.Instance?.Initialize(config);
        SetMatchState(MatchState.Playing);
    }

    // Démarre une configuration dev par défaut au premier tick si aucun initialiseur n'existe.
    public void TryStartFallbackAtFirstTick()
    {
        if (CurrentMatchState == MatchState.Setup && FindAnyObjectByType<MatchInitializer>() == null)
            StartPlaying(MatchConfig.CreateDefault());
    }

    // Termine définitivement la partie et publie les vainqueurs sans créer d'UI.
    public void EndMatch(int winnerAllianceID, System.Collections.Generic.IReadOnlyList<int> winnerPlayerIDs)
    {
        if (CurrentMatchState != MatchState.Playing)
            return;

        if (GameLogger.IsEnabled(GameLogger.Category.Match))
        {
            if (winnerAllianceID == VictorySystem.DRAW_ALLIANCE_ID)
            {
                GameLogger.Log(GameLogger.Category.Match, "Fin de partie — match nul, aucune alliance survivante.", this);
            }
            else
            {
                string winnerIDs = winnerPlayerIDs != null ? string.Join(", ", winnerPlayerIDs) : string.Empty;
                GameLogger.Log(GameLogger.Category.Match,
                    $"Fin de partie — alliance {winnerAllianceID} gagnante, joueurs [{winnerIDs}]", this);
            }
        }

        SetMatchState(MatchState.Ended);
        OnMatchEnded.Invoke(winnerAllianceID, winnerPlayerIDs ?? System.Array.Empty<int>());
    }

    // Indique si VictorySystem a définitivement éliminé ce joueur.
    public bool IsPlayerEliminated(int playerID)
    {
        return VictoryManager.Instance != null && VictoryManager.Instance.IsPlayerEliminated(playerID);
    }

    // Applique une transition et notifie ses observateurs.
    private void SetMatchState(MatchState state)
    {
        if (CurrentMatchState == state)
            return;

        MatchState previousState = CurrentMatchState;
        CurrentMatchState = state;
        if (GameLogger.IsEnabled(GameLogger.Category.Match))
        {
            GameLogger.Log(GameLogger.Category.Match,
                $"État de partie {previousState} → {CurrentMatchState}", this);
        }
        OnMatchStateChanged.Invoke(state);
    }
}
