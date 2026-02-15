using UnityEngine;

// Gère l'appartenance d'une unité à une équipe
// Permet de déterminer les relations ennemi/allié entre unités
// Prépare le système d'alliances pour multi-joueurs futur
public class TeamComponent : MonoBehaviour
{
    [Header("Team Settings")]
    [SerializeField] private int teamID = 1; // 0 = Neutre, 1-6 = Joueurs

    [Header("Debug")]
    [SerializeField] private bool debugTeam = false;

    // Initialise le composant
    private void Awake()
    {
        if (debugTeam)
        {
            Debug.Log($"TeamComponent {gameObject.name}: Équipe {teamID}");
        }
    }

    // Vérifie si une autre unité est ennemie
    // Retourne true si les équipes sont différentes et non-neutres
    public bool IsEnemy(GameObject otherUnit)
    {
        if (otherUnit == null)
        {
            return false;
        }

        TeamComponent otherTeam = otherUnit.GetComponent<TeamComponent>();
        if (otherTeam == null)
        {
            if (debugTeam)
            {
                Debug.LogWarning($"TeamComponent {gameObject.name}: {otherUnit.name} n'a pas de TeamComponent");
            }
            return false;
        }

        // Neutres (teamID = 0) ne sont ennemis avec personne
        if (teamID == 0 || otherTeam.teamID == 0)
        {
            return false;
        }

        // TODO: Vérifier alliances ici quand implémenté
        // Pour l'instant : équipes différentes = ennemis
        bool isEnemy = teamID != otherTeam.teamID;

        if (debugTeam && isEnemy)
        {
            Debug.Log($"TeamComponent {gameObject.name} (Team {teamID}): {otherUnit.name} (Team {otherTeam.teamID}) est ENNEMI");
        }

        return isEnemy;
    }

    // Vérifie si une autre unité est alliée
    // Retourne true si même équipe ou alliance active
    public bool IsAlly(GameObject otherUnit)
    {
        if (otherUnit == null)
        {
            return false;
        }

        // Une unité est toujours alliée avec elle-même
        if (otherUnit == gameObject)
        {
            return true;
        }

        TeamComponent otherTeam = otherUnit.GetComponent<TeamComponent>();
        if (otherTeam == null)
        {
            if (debugTeam)
            {
                Debug.LogWarning($"TeamComponent {gameObject.name}: {otherUnit.name} n'a pas de TeamComponent");
            }
            return false;
        }

        // Neutres ne sont alliés avec personne (sauf eux-mêmes)
        if (teamID == 0 || otherTeam.teamID == 0)
        {
            return false;
        }

        // TODO: Vérifier alliances ici quand implémenté
        // Pour l'instant : même équipe = alliés
        bool isAlly = teamID == otherTeam.teamID;

        if (debugTeam && isAlly)
        {
            Debug.Log($"TeamComponent {gameObject.name} (Team {teamID}): {otherUnit.name} (Team {otherTeam.teamID}) est ALLIÉ");
        }

        return isAlly;
    }

    // Vérifie si l'unité est neutre
    // Les unités neutres n'attaquent ni ne sont attaquées automatiquement
    public bool IsNeutral()
    {
        return teamID == 0;
    }

    // Retourne l'ID de l'équipe
    public int GetTeamID()
    {
        return teamID;
    }

    // Modifie l'ID de l'équipe (pour changement d'équipe en jeu)
    public void SetTeamID(int newTeamID)
    {
        teamID = Mathf.Clamp(newTeamID, 0, 6); // Limite à 6 joueurs + neutre

        if (debugTeam)
        {
            Debug.Log($"TeamComponent {gameObject.name}: Équipe changée vers {teamID}");
        }
    }

    // Retourne une description textuelle de l'équipe
    public string GetTeamName()
    {
        if (teamID == 0)
        {
            return "Neutre";
        }
        else
        {
            return $"Joueur {teamID}";
        }
    }

    // Méthode de debug pour afficher les infos d'équipe
    [ContextMenu("Debug Team Info")]
    private void DebugTeamInfo()
    {
        Debug.Log($"=== TeamComponent Debug - {gameObject.name} ===");
        Debug.Log($"Team ID: {teamID}");
        Debug.Log($"Team Name: {GetTeamName()}");
        Debug.Log($"Est Neutre: {IsNeutral()}");
    }
}
