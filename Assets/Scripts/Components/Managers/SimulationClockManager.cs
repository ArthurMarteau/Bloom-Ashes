using UnityEngine;

// Pont MonoBehaviour entre FixedUpdate et l'horloge logique POCO
[DefaultExecutionOrder(-90)]
public class SimulationClockManager : MonoBehaviour
{
    public static SimulationClockManager Instance { get; private set; }

    private readonly SimulationClock clock = new SimulationClock();

    public long CurrentTick => clock.CurrentTick;

    // Crée le manager si la scène chargée n'en fournit pas
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void CreateRuntimeInstance()
    {
        if (Instance != null)
            return;

        GameObject managerObject = new GameObject(nameof(SimulationClockManager));
        managerObject.AddComponent<SimulationClockManager>();
        DontDestroyOnLoad(managerObject);
    }

    // Initialise l'instance MonoBehaviour unique
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    // Avance l'horloge à fréquence fixe Unity
    private void FixedUpdate()
    {
        clock.Advance();
    }

    // Nettoie la référence globale lors de la destruction
    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }
}
