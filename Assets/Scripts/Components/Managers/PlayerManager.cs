using System.Collections.Generic;
using UnityEngine;

// Gère la configuration des joueurs de la session et expose le propriétaire local
// S'initialise avant les autres managers grâce à DefaultExecutionOrder
[DefaultExecutionOrder(-75)]
public class PlayerManager : MonoBehaviour
{
    public static PlayerManager Instance { get; private set; }

    private static readonly Color[] DefaultPlayerColors =
    {
        Color.white,
        new Color(0f, 0.3724656f, 1f),
        new Color(1f, 0.0030752914f, 0f),
        new Color(0f, 1f, 0.037088156f),
        new Color(0.98326576f, 1f, 0f),
        new Color(0.5655279f, 0f, 1f),
        new Color(1f, 0.5571841f, 0f)
    };

    // ID du joueur local, conservé à 1 pour reproduire la scène actuelle.
    [SerializeField] private int localPlayerID = 1;

    // Palette indexée par PlayerSlot.colorIndex.
    [SerializeField] private Color[] playerColors = CreateDefaultPlayerColors();

    private MatchConfig matchConfig;
    private readonly AllianceSystem allianceSystem = new AllianceSystem();

    // Retourne la configuration active en lecture seule.
    public MatchConfig CurrentMatchConfig => matchConfig;

    // Retourne l'identifiant du propriétaire local issu de la configuration du manager.
    public int LocalPlayerID => localPlayerID;

    // Initialise le singleton
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        if (matchConfig == null)
        {
            Initialize(MatchConfig.CreateDefault());
            GameLogger.Log(GameLogger.Category.Match, "👥 PlayerManager : aucune MatchConfig reçue, configuration 1v1 par défaut appliquée.", this);
        }
    }

    // Initialise les slots et les alliances depuis les données partagées du match.
    public void Initialize(MatchConfig config)
    {
        matchConfig = config ?? MatchConfig.CreateDefault();
        matchConfig.ClampValues();
        allianceSystem.Initialize(matchConfig);

        if (GetPlayerSlot(localPlayerID) == null && matchConfig.players != null && matchConfig.players.Length > 0)
            localPlayerID = matchConfig.players[0].playerID;
    }

    // Enregistre chaque joueur auprès du WatersapManager
    private void Start()
    {
        if (matchConfig?.players == null || WatersapManager.Instance == null) return;

        foreach (PlayerSlot player in matchConfig.players)
        {
            if (player == null)
            {
                GameLogger.Warn(GameLogger.Category.Match, "👥 PlayerManager : un slot joueur est null, ignoré.", this);
                continue;
            }

            WatersapManager.Instance.RegisterTeam(player.playerID);
            GameLogger.Log(GameLogger.Category.Match, $"👥 PlayerManager : joueur {player.playerID} (alliance {player.allianceID}) enregistré.", this);
        }
    }

    // Retourne vrai si l'équipe donnée correspond au joueur local
    public bool IsLocalPlayer(int teamID)
    {
        // Égalité stricte : le contrôle local dépend du propriétaire, pas de son alliance.
        return teamID == localPlayerID;
    }

    // Retourne vrai si l'équipe donnée est contrôlée par un humain
    public bool IsHumanPlayer(int teamID)
    {
        PlayerSlot slot = GetPlayerSlot(teamID);
        if (slot == null)
        {
            GameLogger.Warn(GameLogger.Category.Match, $"👥 PlayerManager : IsHumanPlayer — équipe {teamID} non trouvée.", this);
            return false;
        }
        return !slot.isAI;
    }

    // Retourne le slot de configuration correspondant au propriétaire demandé.
    public PlayerSlot GetPlayerSlot(int teamID)
    {
        if (matchConfig?.players == null) return null;

        foreach (PlayerSlot player in matchConfig.players)
        {
            // Égalité stricte : recherche d'un propriétaire précis.
            if (player != null && player.playerID == teamID)
                return player;
        }
        return null;
    }

    // Retourne la couleur de palette configurée pour un joueur.
    public Color GetPlayerColor(int teamID)
    {
        PlayerSlot slot = GetPlayerSlot(teamID);
        if (slot != null && playerColors != null && slot.colorIndex >= 0 && slot.colorIndex < playerColors.Length)
            return playerColors[slot.colorIndex];

        return Color.white;
    }

    // Retourne directement une couleur de palette à partir du colorIndex sérialisé.
    public Color GetPlayerColorByIndex(int colorIndex)
    {
        return playerColors != null && colorIndex > 0 && colorIndex < playerColors.Length
            ? playerColors[colorIndex]
            : Color.white;
    }

    // Retourne une couleur de la palette par défaut sans exiger une instance de scène.
    public static Color GetDefaultPlayerColorByIndex(int colorIndex)
    {
        return colorIndex > 0 && colorIndex < DefaultPlayerColors.Length
            ? DefaultPlayerColors[colorIndex]
            : Color.white;
    }

    // Crée une copie sérialisable afin de ne pas exposer le tableau par défaut partagé.
    private static Color[] CreateDefaultPlayerColors()
    {
        return (Color[])DefaultPlayerColors.Clone();
    }

    // Retourne l'alliance statique associée à une équipe.
    public int GetAlliance(int teamID) => allianceSystem.GetAlliance(teamID);

    // Retourne vrai si les deux propriétaires partagent une alliance jouable.
    public bool AreAllied(int firstTeamID, int secondTeamID) => allianceSystem.AreAllied(firstTeamID, secondTeamID);

    // Retourne vrai si les deux propriétaires doivent être traités comme hostiles.
    public bool AreEnemies(int firstTeamID, int secondTeamID) => allianceSystem.AreEnemies(firstTeamID, secondTeamID);

    // Retourne vrai si la cible peut recevoir un ordre d'attaque explicite.
    public bool CanBeAttacked(int attackerTeamID, int targetTeamID) => allianceSystem.CanBeAttacked(attackerTeamID, targetTeamID);

    // Affiche la liste de tous les joueurs dans la console
    [ContextMenu("Debug: List All Players")]
    private void DebugListAllPlayers()
    {
        if (matchConfig?.players == null || matchConfig.players.Length == 0)
        {
            GameLogger.Log(GameLogger.Category.Match, "👥 PlayerManager : aucun joueur dans la liste.", this);
            return;
        }

        GameLogger.Log(GameLogger.Category.Match, $"👥 PlayerManager : {matchConfig.players.Length} joueur(s) dans la session :", this);
        foreach (PlayerSlot player in matchConfig.players)
        {
            if (player == null)
            {
                GameLogger.Log(GameLogger.Category.Match, "  — slot null", this);
                continue;
            }
            string type = player.isAI ? "IA" : "Humain";
            string local = player.playerID == localPlayerID ? " ← LOCAL" : "";
            GameLogger.Log(GameLogger.Category.Match, $"  — [{player.playerID}] alliance {player.allianceID} ({type}){local}", this);
        }
    }
}
