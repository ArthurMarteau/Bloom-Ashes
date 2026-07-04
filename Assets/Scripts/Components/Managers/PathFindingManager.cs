using System;
using System.Collections.Generic;
using UnityEngine;

// Pont MonoBehaviour entre la boucle Unity et le scheduler POCO de pathfinding
public class PathFindingManager : MonoBehaviour
{
    public static PathFindingManager Instance { get; private set; }

    [SerializeField] private int pathsPerFrame = 4;

    private readonly PathFindingScheduler scheduler = new PathFindingScheduler();

    // Crée automatiquement le wrapper MonoBehaviour si la scène chargée n'en fournit pas
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void CreateRuntimeInstance()
    {
        if (Instance != null)
            return;

        GameObject managerObject = new GameObject(nameof(PathFindingManager));
        managerObject.AddComponent<PathFindingManager>();
        DontDestroyOnLoad(managerObject);
    }

    // Initialise l'instance unique du manager
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    // Traite le budget de chemins de la frame courante
    private void Update()
    {
        scheduler.ProcessBudget(Mathf.Max(0, pathsPerFrame));
    }

    // Nettoie la référence globale lors de la destruction
    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    // Ajoute une demande de chemin à la file budgétée
    public long RequestPath(Vector2Int start, Vector2Int end, GameObject unit, Action<List<Vector2Int>> callback)
    {
        PathRequest request = new PathRequest
        {
            start = start,
            end = end,
            unit = unit,
            callback = callback
        };

        return scheduler.Enqueue(request);
    }

    // Annule une demande de chemin encore en attente
    public void CancelRequest(long requestId)
    {
        scheduler.Cancel(requestId);
    }
}
