// Interface abstraisant l'appartenance à une équipe
// Permet aux bâtiments ET aux unités d'être comparés en termes d'équipe sans couplage à TeamComponent
// Implémentée par : TeamComponent (Phase 4.2), BuildingStatsComponent (Phase 4.3)
public interface ITeamMember
{
    // Retourne l'ID de l'équipe (-1 = Wildlife hostile, 1-6 = Joueurs)
    int GetTeamID();

    // Vérifie si une autre entité (unité ou bâtiment) est ennemie via interface
    bool IsEnemy(ITeamMember other);

    // Vérifie si une autre entité (unité ou bâtiment) est alliée via interface
    bool IsAlly(ITeamMember other);
}
