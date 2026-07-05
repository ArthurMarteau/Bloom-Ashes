using System;

// Horloge logique et source aléatoire déterministe de la simulation
// Le seed devra être fourni et partagé par le match lors de la migration multijoueur
// Le mouvement et le budget pathfinding sont pilotés par cette horloge depuis N2
public sealed class SimulationClock
{
    public const int TICKS_PER_SECOND = 50;

    private const int DefaultSeed = 12345;
    private Random rng = new Random(DefaultSeed);

    public long CurrentTick { get; private set; }

    // Avance la simulation d'un tick logique fixe
    public void Advance()
    {
        CurrentTick++;
    }

    // Réinitialise la séquence aléatoire avec le seed commun du match
    public void Reseed(int seed)
    {
        rng = new Random(seed);
    }

    // Retourne un entier déterministe dans l'intervalle [minInclusive, maxExclusive[
    public int NextRange(int minInclusive, int maxExclusive)
    {
        return rng.Next(minInclusive, maxExclusive);
    }

    // Convertit une durée en secondes vers le nombre de ticks fixe le plus proche
    public static int SecondsToTicks(float seconds)
    {
        if (seconds <= 0f)
            return 0;

        return Math.Max(1, (int)Math.Round(seconds * TICKS_PER_SECOND, MidpointRounding.AwayFromZero));
    }
}
