using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "New Foundation Stats", menuName = "Bloom and Ashes/Data/Foundation Stats")]
public class FoundationStatsData : EnvironmentStatsData
{
    [Header("Prefab")]
    [Tooltip("Prefab Foundation à respawner quand un bâtiment construit est détruit")]
    [SerializeField] public GameObject prefab;

    [Header("Construction")]
    [Tooltip("Bâtiments constructibles depuis cette Foundation (5 slots maximum, entrées null autorisées)")]
    public List<BuildingStatsData> constructibleBuildings = new List<BuildingStatsData>(5);

    private void OnValidate()
    {
        if (constructibleBuildings == null)
            constructibleBuildings = new List<BuildingStatsData>(5);

        while (constructibleBuildings.Count > 5)
            constructibleBuildings.RemoveAt(constructibleBuildings.Count - 1);
    }
}
