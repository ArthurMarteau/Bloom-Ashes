using UnityEngine;

[CreateAssetMenu(fileName = "WatersapData", menuName = "Bloom and Ashes/Data/Watersap")]
public class WatersapData : ScriptableObject
{
    // Quantité maximale de watersap stockable
    public int maxWatersap = 300;

    // Intervalle en secondes entre chaque revenu passif
    [SerializeField] public float passiveIncomeInterval = 5f;

    // Quantité de watersap gagnée à chaque intervalle passif
    public int passiveIncomeAmount = 1;
}
