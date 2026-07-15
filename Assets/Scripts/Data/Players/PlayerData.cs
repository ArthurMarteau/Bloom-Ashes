using UnityEngine;

[CreateAssetMenu(fileName = "PlayerData", menuName = "Bloom and Ashes/Data/Player")]
public class PlayerData : ScriptableObject
{
    // Identifiant d'équipe du joueur (1 à 6)
    [Range(1, 6)]
    public int teamID = 1;

    // Clamp teamID entre 1 et 6 à la validation depuis l'inspecteur
    private void OnValidate()
    {
        teamID = Mathf.Clamp(teamID, 1, 6);
    }
}
