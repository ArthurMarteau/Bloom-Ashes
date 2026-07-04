// Horloge logique minimale de la simulation.
// Dette netcode : migrer ultérieurement les cooldowns basés sur Time.time,
// les déplacements basés sur Time.deltaTime et les attentes WaitForSeconds.
public sealed class SimulationClock
{
    public long CurrentTick { get; private set; }

    // Avance la simulation d'un tick logique fixe
    public void Advance()
    {
        CurrentTick++;
    }
}
