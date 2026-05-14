using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

// Gère les ressources watersap de chaque équipe (jusqu'à 6 joueurs)
// S'initialise avant les autres managers grâce à DefaultExecutionOrder
// Délègue tous les calculs à WatersapSystem (POCO pur)
[DefaultExecutionOrder(-50)]
public class WatersapManager : MonoBehaviour
{
    public static WatersapManager Instance { get; private set; }

    [Header("Configuration")]
    [SerializeField] private WatersapData watersapData;

    [Header("Debug")]
    [SerializeField] private bool debugWatersap;
    [SerializeField] private bool startWithFullWatersap;

    // Stocke le watersap actuel par équipe (teamID 1 à 6)
    private Dictionary<int, int> watersapByTeam = new Dictionary<int, int>();

    // Timer de revenu passif par équipe
    private Dictionary<int, float> passiveTimerByTeam = new Dictionary<int, float>();

    // Système de calcul pur — aucune dépendance Unity
    private WatersapSystem watersapSystem = new WatersapSystem();

    // Événement déclenché à chaque changement de watersap → (teamID, newAmount)
    public UnityEvent<int, int> OnWatersapChanged = new UnityEvent<int, int>();

    // Initialise le singleton
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        if (watersapData == null)
            GameLogger.Error("💧 WatersapManager : WatersapData non assigné !", this);
    }

    // Fait avancer les timers de revenu passif pour toutes les équipes enregistrées
    private void Update()
    {
        if (watersapData == null) return;

        // Copie des clés pour éviter InvalidOperationException si le dictionnaire est modifié pendant l'itération
        var teamIDs = new System.Collections.Generic.List<int>(watersapByTeam.Keys);
        foreach (int teamID in teamIDs)
        {
            passiveTimerByTeam[teamID] += Time.deltaTime;

            if (passiveTimerByTeam[teamID] >= watersapData.passiveIncomeInterval)
            {
                passiveTimerByTeam[teamID] = 0f;
                AddWatersap(teamID, watersapData.passiveIncomeAmount);

                if (debugWatersap)
                    GameLogger.Log(GameLogger.Category.Watersap, $"💧 WatersapManager : revenu passif appliqué à l'équipe {teamID}.", this);
            }
        }
    }

    // Enregistre une équipe et initialise son compteur à zéro
    public void RegisterTeam(int teamID)
    {
        if (watersapByTeam.ContainsKey(teamID))
        {
            GameLogger.Warn(GameLogger.Category.Watersap, $"💧 WatersapManager : équipe {teamID} déjà enregistrée.", this);
            return;
        }

        watersapByTeam[teamID] = 0;
        passiveTimerByTeam[teamID] = 0f;

        if (startWithFullWatersap && watersapData != null)
        {
            watersapByTeam[teamID] = watersapData.maxWatersap;
            OnWatersapChanged.Invoke(teamID, watersapData.maxWatersap);
        }

        if (debugWatersap)
            GameLogger.Log(GameLogger.Category.Watersap, $"💧 WatersapManager : équipe {teamID} enregistrée.", this);
    }

    // Retourne le watersap maximum (lu depuis le SO)
    public int MaxWatersap => watersapData != null ? watersapData.maxWatersap : 300;

    // Retourne le watersap actuel d'une équipe
    public int GetWatersap(int teamID)
    {
        if (!watersapByTeam.TryGetValue(teamID, out int current))
        {
            GameLogger.Warn(GameLogger.Category.Watersap, $"💧 WatersapManager : GetWatersap — équipe {teamID} non enregistrée.", this);
            return 0;
        }

        return current;
    }

    // Ajoute du watersap à une équipe (sacrifice, collecte Rootborn, revenu passif)
    public void AddWatersap(int teamID, int amount)
    {
        if (!watersapByTeam.TryGetValue(teamID, out int current))
        {
            GameLogger.Warn(GameLogger.Category.Watersap, $"💧 WatersapManager : AddWatersap — équipe {teamID} non enregistrée.", this);
            return;
        }

        int newAmount = watersapSystem.CalculateNewAmount(current, amount, watersapData.maxWatersap);
        watersapByTeam[teamID] = newAmount;

        if (debugWatersap)
            GameLogger.Log(GameLogger.Category.Watersap, $"💧 WatersapManager : équipe {teamID} +{amount} watersap → {newAmount}/{watersapData.maxWatersap}", this);

        OnWatersapChanged.Invoke(teamID, newAmount);
    }

    // Vérifie si une équipe peut se permettre un montant donné sans le dépenser
    public bool CanSpend(int teamID, int amount)
    {
        if (!watersapByTeam.TryGetValue(teamID, out int current))
        {
            GameLogger.Warn(GameLogger.Category.Watersap, $"💧 WatersapManager : CanSpend — équipe {teamID} non enregistrée.", this);
            return false;
        }

        return watersapSystem.CanAfford(current, amount);
    }

    // Tente de dépenser du watersap — retourne false si le solde est insuffisant
    public bool SpendWatersap(int teamID, int amount)
    {
        if (!watersapByTeam.TryGetValue(teamID, out int current))
        {
            GameLogger.Warn(GameLogger.Category.Watersap, $"💧 WatersapManager : SpendWatersap — équipe {teamID} non enregistrée.", this);
            return false;
        }

        if (!watersapSystem.CanAfford(current, amount))
        {
            GameLogger.Warn(GameLogger.Category.Watersap, $"💧 WatersapManager : équipe {teamID} ne peut pas se permettre {amount} watersap (solde : {current}).", this);
            return false;
        }

        int newAmount = watersapSystem.CalculateSpend(current, amount, watersapData.maxWatersap);
        watersapByTeam[teamID] = newAmount;

        if (debugWatersap)
            GameLogger.Log(GameLogger.Category.Watersap, $"💧 WatersapManager : équipe {teamID} -{amount} watersap → {newAmount}/{watersapData.maxWatersap}", this);

        OnWatersapChanged.Invoke(teamID, newAmount);
        return true;
    }

    // Teste l'ajout de 50 watersap à l'équipe 1 depuis l'éditeur
    [ContextMenu("Test: Add 50 to Team 1")]
    private void TestAdd50ToTeam1()
    {
        if (!watersapByTeam.ContainsKey(1)) RegisterTeam(1);
        AddWatersap(1, 50);
    }

    // Teste la dépense de 50 watersap depuis l'équipe 1 depuis l'éditeur
    [ContextMenu("Test: Spend 50 from Team 1")]
    private void TestSpend50FromTeam1()
    {
        if (!watersapByTeam.ContainsKey(1)) RegisterTeam(1);
        SpendWatersap(1, 50);
    }
}
