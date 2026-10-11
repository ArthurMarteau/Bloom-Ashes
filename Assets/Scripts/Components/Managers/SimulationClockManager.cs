using UnityEngine;

// Pont MonoBehaviour entre FixedUpdate et l'horloge logique POCO
[DefaultExecutionOrder(-90)]
public class SimulationClockManager : MonoBehaviour
{
    public static SimulationClockManager Instance { get; private set; }

    private readonly SimulationClock clock = new SimulationClock();
    private readonly SimulationTickManager tickManager = new SimulationTickManager();
    private readonly PendingDamageResolver pendingDamageResolver = new PendingDamageResolver();
    private float lastFixedUpdateTime;

    public long CurrentTick => clock.CurrentTick;
    public SimulationClock SimulationRandom => clock;
    public PendingDamageResolver PendingDamage => pendingDamageResolver;
    public float InterpolationAlpha => Mathf.Clamp01(
        (Time.time - lastFixedUpdateTime) / Mathf.Max(Time.fixedDeltaTime, 0.0001f));

    // Réinitialise la source aléatoire avec la graine partagée du match.
    public void Reseed(int seed)
    {
        clock.Reseed(seed);
    }

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
        NetworkManager.Instance?.Poll();

        long nextTick = clock.CurrentTick + 1;
        LockstepController lockstep = NetworkManager.Instance?.Lockstep;
        if (lockstep != null)
        {
            lockstep.PrepareTick(nextTick);
            if (!lockstep.CanAdvance(nextTick))
                return;
        }

        clock.Advance();
        GameCommand[] commands = lockstep != null
            ? lockstep.GetCommandsForTick(clock.CurrentTick)
            : System.Array.Empty<GameCommand>();
        CommandExecutor.Instance?.ExecuteTick(clock.CurrentTick, commands);
        tickManager.TickAll(clock.CurrentTick);
        pendingDamageResolver.SimTick(clock.CurrentTick);
        PathFindingManager.Instance?.ProcessTickBudget();
        NetworkManager.Instance?.LogChecksum(clock.CurrentTick);
        lastFixedUpdateTime = Time.time;
    }

    // Enregistre un participant de simulation sous son identifiant stable
    public void Register(long entityID, ITickable tickable)
    {
        tickManager.Register(entityID, tickable);
    }

    // Retire tous les participants associés à un identifiant
    public void Unregister(long entityID)
    {
        tickManager.Unregister(entityID);
    }

    // Retire un composant précis sans affecter les autres composants de l'entité
    public void Unregister(long entityID, ITickable tickable)
    {
        tickManager.Unregister(entityID, tickable);
    }

    // Programme un dégât qui survivra au retrait de l'attaquant de la simulation
    public void ScheduleDamage(long targetEntityID, long attackerEntityID, int damage, long atTick)
    {
        pendingDamageResolver.Schedule(targetEntityID, attackerEntityID, damage, atTick);
    }

    // Nettoie la référence globale lors de la destruction
    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }
}
