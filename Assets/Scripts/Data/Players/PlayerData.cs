using UnityEngine;

[CreateAssetMenu(fileName = "PlayerData", menuName = "Bloom and Ashes/Data/Player")]
public class PlayerData : ScriptableObject
{
    // Identifiant d'équipe du joueur (1 à 6)
    [Range(1, 6)]
    public int teamID = 1;

    // Nom affiché dans l'interface
    public string displayName = "Player";

    // Couleur associée à l'équipe dans l'interface et sur la carte
    public Color teamColor = Color.white;

    // Vrai si ce joueur est humain, faux si contrôlé par l'IA
    public bool isHuman = true;

    // Clamp teamID entre 1 et 6 à la validation depuis l'inspecteur
    private void OnValidate()
    {
        teamID = Mathf.Clamp(teamID, 1, 6);
    }
}
