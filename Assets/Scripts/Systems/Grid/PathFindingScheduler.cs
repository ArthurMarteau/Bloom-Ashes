using System;
using System.Collections.Generic;
using UnityEngine;

// Requête de chemin transportée sans interpréter le GameObject côté scheduler
public struct PathRequest
{
    public Vector2Int start;
    public Vector2Int end;
    public GameObject unit;
    public Func<Vector2Int, bool> isWalkable;
    public Action<List<Vector2Int>> callback;
    public long requestId;
}

// Planificateur POCO qui limite le nombre de calculs A* exécutés par tick
public class PathFindingScheduler
{
    private readonly Queue<PathRequest> pendingRequests = new Queue<PathRequest>();
    private readonly Dictionary<GameObject, long> activeRequestsByUnit = new Dictionary<GameObject, long>();
    private readonly Dictionary<long, GameObject> requestUnits = new Dictionary<long, GameObject>();
    private readonly HashSet<long> canceledRequests = new HashSet<long>();
    private readonly Func<Vector2Int, Vector2Int, GameObject, List<Vector2Int>> pathCalculator;
    private long nextRequestId = 1;

    // Accepte un calculateur injecté pour permettre les tests sans scène Unity
    public PathFindingScheduler(Func<Vector2Int, Vector2Int, GameObject, List<Vector2Int>> customPathCalculator = null)
    {
        pathCalculator = customPathCalculator ?? PathFinding.FindPath;
    }

    // Ajoute une requête et remplace automatiquement celle déjà en attente pour la même unité
    public long Enqueue(PathRequest request)
    {
        long requestId = nextRequestId++;

        if (request.unit != null && activeRequestsByUnit.TryGetValue(request.unit, out long previousRequestId))
            Cancel(previousRequestId);

        request.requestId = requestId;
        pendingRequests.Enqueue(request);
        requestUnits[requestId] = request.unit;

        if (request.unit != null)
        {
            activeRequestsByUnit[request.unit] = requestId;
        }

        return requestId;
    }

    // Annule une requête en attente sans parcourir la queue
    public void Cancel(long requestId)
    {
        if (!requestUnits.TryGetValue(requestId, out GameObject unit))
            return;

        canceledRequests.Add(requestId);
        requestUnits.Remove(requestId);

        if (!ReferenceEquals(unit, null)
            && activeRequestsByUnit.TryGetValue(unit, out long activeRequestId)
            && activeRequestId == requestId)
        {
            activeRequestsByUnit.Remove(unit);
        }
    }

    // Exécute au maximum le budget demandé et ignore les requêtes annulées ou détruites
    public void ProcessBudget(int maxPerTick)
    {
        int processedCount = 0;

        while (processedCount < maxPerTick && pendingRequests.Count > 0)
        {
            PathRequest request = pendingRequests.Dequeue();

            if (canceledRequests.Remove(request.requestId))
                continue;

            requestUnits.Remove(request.requestId);
            if (!ReferenceEquals(request.unit, null)
                && activeRequestsByUnit.TryGetValue(request.unit, out long activeRequestId)
                && activeRequestId == request.requestId)
            {
                activeRequestsByUnit.Remove(request.unit);
            }

            if (request.unit == null && request.isWalkable == null)
                continue;

            List<Vector2Int> path = request.isWalkable != null
                ? PathFinding.FindStaticPath(request.start, request.end, request.isWalkable)
                : pathCalculator(request.start, request.end, request.unit);
            request.callback?.Invoke(path);
            processedCount++;
        }
    }
}
