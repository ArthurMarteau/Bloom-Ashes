using System;
using System.Collections.Generic;

// Contrat des participants à la boucle logique fixe
public interface ITickable
{
    void SimTick(long tick);
}

// Ordonne les ticks par identifiant d'entité puis par type de composant
public sealed class SimulationTickManager
{
    private readonly SortedDictionary<long, ITickable> tickables = new SortedDictionary<long, ITickable>();

    // Enregistre un participant sous l'identifiant stable de son entité
    public void Register(long entityID, ITickable tickable)
    {
        if (entityID < 0 || tickable == null)
            return;

        if (!tickables.TryGetValue(entityID, out ITickable existing))
        {
            tickables[entityID] = tickable;
            return;
        }

        if (ReferenceEquals(existing, tickable))
            return;

        if (existing is TickGroup group)
        {
            group.Register(tickable);
            return;
        }

        TickGroup newGroup = new TickGroup();
        newGroup.Register(existing);
        newGroup.Register(tickable);
        tickables[entityID] = newGroup;
    }

    // Désenregistre tous les participants d'une entité
    public void Unregister(long entityID)
    {
        tickables.Remove(entityID);
    }

    // Désenregistre un seul composant sans retirer les autres composants de l'entité
    public void Unregister(long entityID, ITickable tickable)
    {
        if (!tickables.TryGetValue(entityID, out ITickable existing))
            return;

        if (ReferenceEquals(existing, tickable))
        {
            tickables.Remove(entityID);
            return;
        }

        if (existing is TickGroup group)
        {
            group.Unregister(tickable);
            if (group.Count == 0)
                tickables.Remove(entityID);
        }
    }

    // Exécute une copie stable pour tolérer les inscriptions et retraits pendant le tick
    public void TickAll(long tick)
    {
        List<ITickable> snapshot = new List<ITickable>(tickables.Values);
        foreach (ITickable tickable in snapshot)
        {
            tickable.SimTick(tick);
        }
    }

    // Regroupe les composants partageant un entityID dans un ordre de type stable
    private sealed class TickGroup : ITickable
    {
        private readonly SortedDictionary<string, ITickable> members = new SortedDictionary<string, ITickable>(StringComparer.Ordinal);

        public int Count => members.Count;

        // Ajoute ou remplace le composant de ce type
        public void Register(ITickable tickable)
        {
            members[GetStableTypeName(tickable)] = tickable;
        }

        // Retire uniquement l'instance demandée
        public void Unregister(ITickable tickable)
        {
            string typeName = GetStableTypeName(tickable);
            if (members.TryGetValue(typeName, out ITickable existing) && ReferenceEquals(existing, tickable))
                members.Remove(typeName);
        }

        // Exécute une copie stable des composants de l'entité
        public void SimTick(long tick)
        {
            List<ITickable> snapshot = new List<ITickable>(members.Values);
            foreach (ITickable member in snapshot)
            {
                member.SimTick(tick);
            }
        }

        // Produit la clé de tri déterministe d'un type de composant
        private static string GetStableTypeName(ITickable tickable)
        {
            Type type = tickable.GetType();
            return type.FullName ?? type.Name;
        }
    }
}
