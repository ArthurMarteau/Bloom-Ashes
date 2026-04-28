using System.Collections.Generic;
using UnityEngine;

// Affiche les 10 cells de stock Watersap au-dessus du portrait du Rootborn
// Chaque cell contient un GameObject "Watersap_On" qui s'active/désactive selon le stock
// Visible uniquement quand un Rootborn (avec RootbornStockComponent) est sélectionné
public class WatersapStockUI : MonoBehaviour
{
    [Header("Container")]
    [Tooltip("Container parent des 10 cells. SetActive(false) si pas un Rootborn.")]
    [SerializeField] private GameObject container;

    [Header("Cells On (knobs bleus)")]
    [Tooltip("Les 10 GameObjects 'Watersap_On' à activer/désactiver. Index 0 = première à se remplir.")]
    [SerializeField] private List<GameObject> cellsOn = new List<GameObject>();

    private RootbornStockComponent trackedStock;

    // Affiche le UI pour un Rootborn donné
    public void Show(RootbornStockComponent stockComponent)
    {
        UntrackCurrent();

        if (stockComponent == null)
        {
            Hide();
            return;
        }

        trackedStock = stockComponent;
        trackedStock.OnStockChanged.AddListener(RefreshCells);

        if (container != null) container.SetActive(true);
        RefreshCells(trackedStock.CurrentStock);
    }

    // Masque le UI
    public void Hide()
    {
        UntrackCurrent();
        if (container != null) container.SetActive(false);
    }

    // Active/désactive les Watersap_On selon le stock courant
    private void RefreshCells(int currentStock)
    {
        for (int i = 0; i < cellsOn.Count; i++)
        {
            if (cellsOn[i] == null) continue;
            cellsOn[i].SetActive(i < currentStock);
        }
    }

    // Désabonne l'event du Rootborn tracké
    private void UntrackCurrent()
    {
        if (trackedStock != null)
        {
            trackedStock.OnStockChanged.RemoveListener(RefreshCells);
            trackedStock = null;
        }
    }

    // Désabonne à la destruction pour éviter les fuites d'event
    private void OnDestroy()
    {
        UntrackCurrent();
    }
}
