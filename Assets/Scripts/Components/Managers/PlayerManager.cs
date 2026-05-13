using System.Collections.Generic;
using UnityEngine;

// Gère la liste des joueurs de la session et expose le joueur local
// S'initialise avant les autres managers grâce à DefaultExecutionOrder
[DefaultExecutionOrder(-75)]
public class PlayerManager : MonoBehaviour
{
    public static PlayerManager Instance { get; private set; }

    [Header("Configuration")]
    [SerializeField] private List<PlayerData> players;

    // Index du joueur local dans la liste (permet de switcher en dev)
    [SerializeField] private int localPlayerIndex = 0;

    // Retourne les données du joueur local
    public PlayerData LocalPlayer => players[localPlayerIndex];

    // Retourne la liste complète des joueurs (lecture seule)
    public List<PlayerData> AllPlayers => players;

    // Initialise le singleton
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        if (players == null || players.Count == 0)
            Debug.LogError("👥 PlayerManager : aucun joueur assigné dans l'inspecteur !");
    }

    // Enregistre chaque joueur auprès du WatersapManager
    private void Start()
    {
        if (players == null) return;

        foreach (PlayerData player in players)
        {
            if (player == null)
            {
                Debug.LogWarning("👥 PlayerManager : un slot joueur est null, ignoré.");
                continue;
            }

            WatersapManager.Instance.RegisterTeam(player.teamID);
            Debug.Log($"👥 PlayerManager : joueur '{player.displayName}' (équipe {player.teamID}) enregistré.");
        }
    }

    // Retourne vrai si l'équipe donnée correspond au joueur local
    public bool IsLocalPlayer(int teamID)
    {
        return teamID == LocalPlayer.teamID;
    }

    // Retourne vrai si l'équipe donnée est contrôlée par un humain
    public bool IsHumanPlayer(int teamID)
    {
        PlayerData data = GetPlayerData(teamID);
        if (data == null)
        {
            Debug.LogWarning($"👥 PlayerManager : IsHumanPlayer — équipe {teamID} non trouvée.");
            return false;
        }
        return data.isHuman;
    }

    // Retourne les données du joueur correspondant à l'équipe donnée, null si non trouvé
    public PlayerData GetPlayerData(int teamID)
    {
        if (players == null) return null;

        foreach (PlayerData player in players)
        {
            if (player != null && player.teamID == teamID)
                return player;
        }
        return null;
    }

    // Affiche la liste de tous les joueurs dans la console
    [ContextMenu("Debug: List All Players")]
    private void DebugListAllPlayers()
    {
        if (players == null || players.Count == 0)
        {
            Debug.Log("👥 PlayerManager : aucun joueur dans la liste.");
            return;
        }

        Debug.Log($"👥 PlayerManager : {players.Count} joueur(s) dans la session :");
        foreach (PlayerData player in players)
        {
            if (player == null)
            {
                Debug.Log("  — slot null");
                continue;
            }
            string type = player.isHuman ? "Humain" : "IA";
            string local = (players.IndexOf(player) == localPlayerIndex) ? " ← LOCAL" : "";
            Debug.Log($"  — [{player.teamID}] {player.displayName} ({type}){local}");
        }
    }
}
