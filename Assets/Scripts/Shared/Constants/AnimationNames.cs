// Noms d'animations centralisés — élimine les chaînes magiques dispersées dans AnimatorController
// Toute référence à un état de l'Animator doit passer par ces constantes
public static class AnimationNames
{
    // Animations de base
    public const string Idle         = "Idle";
    public const string IdleReversed = "Idle_Reversed"; // Spécifique Rootborn
    public const string Death        = "Death";

    // Animations de réaction aux coups
    public const string HitIdle = "Hit_Idle"; // Rootborn en idle
    public const string HitNW   = "Hit_NW";   // Coup venant du haut
    public const string HitSW   = "Hit_SW";   // Coup venant du bas ou du côté

    // Préfixes des animations directionnelles
    public const string PrefixWalk       = "Walk_";
    public const string PrefixAttack     = "Attack_";
    public const string PrefixAttackIdle = "AttackIdle_";

    // Suffixes directionnels
    public const string SuffixN  = "N";
    public const string SuffixNW = "NW";
    public const string SuffixW  = "W";
    public const string SuffixSW = "SW";
    public const string SuffixS  = "S";

    // Noms complets des animations de marche (utilisés dans le dictionnaire de cache)
    public const string WalkN  = PrefixWalk + SuffixN;
    public const string WalkNW = PrefixWalk + SuffixNW;
    public const string WalkW  = PrefixWalk + SuffixW;
    public const string WalkSW = PrefixWalk + SuffixSW;
    public const string WalkS  = PrefixWalk + SuffixS;
}
