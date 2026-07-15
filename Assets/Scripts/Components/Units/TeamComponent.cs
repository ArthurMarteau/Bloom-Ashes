using UnityEngine;

// Gère l'appartenance d'une unité à une équipe
// Permet de déterminer les relations ennemi/allié entre unités
// Prépare le système d'alliances pour multi-joueurs futur
public class TeamComponent : MonoBehaviour, ITeamMember
{
    [Header("Team Settings")]
    [SerializeField] private int teamID = 1; // -1 = Wildlife hostile, 1-6 = Joueurs

    // Couleurs de fallback utilisées si PlayerManager est absent ou si le joueur est inconnu
    private static readonly Color[] fallbackColors = new Color[]
    {
        Color.white,                    // 0 = Neutre
        new Color(0.2f, 0.4f, 1f),      // 1
        Color.red,                      // 2
        Color.green,                    // 3
        Color.yellow,                   // 4
        new Color(1f, 0.5f, 0f),        // 5 (orange)
        Color.magenta                   // 6
    };

    [Header("Debug")]
    [SerializeField] private bool debugTeam = false;

    // Initialise le composant et applique la couleur d'équipe
    private void Awake()
    {
        ApplyTeamColor();

        if (debugTeam)
            GameLogger.Log(GameLogger.Category.Combat, $"🎨 TeamComponent {gameObject.name}: Équipe {teamID}", this);
    }

    // Applique la couleur d'équipe au SpriteRenderer via le shader TeamColor
    private void ApplyTeamColor()
    {
        SpriteRenderer spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer == null)
        {
            if (debugTeam)
                GameLogger.Warn(GameLogger.Category.Combat, $"⚠️ TeamComponent {gameObject.name}: Pas de SpriteRenderer trouvé", this);
            return;
        }

        if (spriteRenderer.material == null)
        {
            if (debugTeam)
                GameLogger.Warn(GameLogger.Category.Combat, $"⚠️ TeamComponent {gameObject.name}: Pas de material trouvé", this);
            return;
        }

        // // Crée une instance du material pour éviter de modifier le material partagé
        spriteRenderer.material = new Material(spriteRenderer.material);
        spriteRenderer.material.SetColor("_TeamColor", GetTeamColor());

        if (debugTeam)
            GameLogger.Log(GameLogger.Category.Combat, $"🎨 TeamComponent {gameObject.name}: Couleur équipe {teamID} appliquée → {GetTeamColor()}", this);
    }

    // Retourne la couleur associée à l'équipe courante
    // Priorité : palette PlayerManager indexée par PlayerSlot.colorIndex, puis fallback local.
    public Color GetTeamColor()
    {
        if (teamID == AllianceSystem.NEUTRAL_ALLIANCE)
            return Color.white;

        if (PlayerManager.Instance != null && PlayerManager.Instance.GetPlayerSlot(teamID) != null)
            return PlayerManager.Instance.GetPlayerColor(teamID);

        // Fallback si PlayerManager absent ou joueur non trouvé
        if (teamID >= 0 && teamID < fallbackColors.Length)
            return fallbackColors[teamID];

        return Color.white;
    }

    // Vérifie si une entité est ennemie via interface ITeamMember
    // Permet de comparer unités ET bâtiments sans GetComponent<TeamComponent>
    public bool IsEnemy(ITeamMember other)
    {
        if (other == null)
            return false;

        bool isEnemy = PlayerManager.Instance != null
            && PlayerManager.Instance.AreEnemies(teamID, other.GetTeamID());

        if (debugTeam && isEnemy)
            GameLogger.Log(GameLogger.Category.Combat, $"TeamComponent {gameObject.name} (Team {teamID}): ITeamMember Team {other.GetTeamID()} est ENNEMI", this);

        return isEnemy;
    }

    // Vérifie si une entité est alliée via interface ITeamMember
    public bool IsAlly(ITeamMember other)
    {
        if (other == null)
            return false;

        // La même instance est toujours alliée à elle-même, y compris pour la wildlife.
        if (other == (ITeamMember)this)
            return true;

        bool isAlly = PlayerManager.Instance != null
            && PlayerManager.Instance.AreAllied(teamID, other.GetTeamID());

        if (debugTeam && isAlly)
            GameLogger.Log(GameLogger.Category.Combat, $"TeamComponent {gameObject.name} (Team {teamID}): ITeamMember Team {other.GetTeamID()} est ALLIÉ", this);

        return isAlly;
    }

    // Vérifie si l'unité est neutre
    public bool IsNeutral()
    {
        return teamID == AllianceSystem.NEUTRAL_ALLIANCE;
    }

    // Retourne l'ID de l'équipe
    public int GetTeamID()
    {
        return teamID;
    }

    // Modifie l'ID de l'équipe et met à jour la couleur
    public void SetTeamID(int newTeamID)
    {
        teamID = Mathf.Clamp(newTeamID, AllianceSystem.NEUTRAL_ALLIANCE, 6);
        ApplyTeamColor();

        if (debugTeam)
            GameLogger.Log(GameLogger.Category.Combat, $"🎨 TeamComponent {gameObject.name}: Équipe changée vers {teamID}", this);
    }

    // Retourne une description textuelle de l'équipe
    public string GetTeamName()
    {
        return teamID == AllianceSystem.NEUTRAL_ALLIANCE ? "Wildlife" : $"Joueur {teamID}";
    }

    // Méthode de debug pour afficher les infos d'équipe
    [ContextMenu("Debug Team Info")]
    private void DebugTeamInfo()
    {
        GameLogger.Log(GameLogger.Category.Combat, $"=== TeamComponent Debug - {gameObject.name} ===", this);
        GameLogger.Log(GameLogger.Category.Combat, $"Team ID: {teamID}", this);
        GameLogger.Log(GameLogger.Category.Combat, $"Team Name: {GetTeamName()}", this);
        GameLogger.Log(GameLogger.Category.Combat, $"Est Neutre: {IsNeutral()}", this);
        GameLogger.Log(GameLogger.Category.Combat, $"Team Color: {GetTeamColor()}", this);
    }
}
