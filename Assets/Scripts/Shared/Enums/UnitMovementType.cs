// Type de déplacement d'une unité
// Utilisé par UnitStatsData et UnitTypeHelper pour la logique terrain/pathfinding
public enum UnitMovementType
{
    Ground,   // Unité terrestre standard
    Flying,   // Unité volante — ignore tous les terrains
    Swimming, // Unité aquatique — peut traverser l'eau et les marécages
}
