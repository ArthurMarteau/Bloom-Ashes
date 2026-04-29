using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// Panneau UI affichant les boutons de construction disponibles pour une Foundation
// Abonné à SelectionManager.OnFoundationSelected — s'affiche uniquement quand un Rootborn
// local sélectionné se trouve sur les cases d'une Foundation
//
// SETUP PREFAB — Hiérarchie attendue :
//   Canvas
//   └── PanelRoot (masqué par défaut)
//       └── ButtonsParent (Layout Group horizontal ou grid)
//           └── [boutons instanciés dynamiquement]
//
// BOUTON PREFAB attendu :
//   Button (root)
//   ├── Portrait (Image — premier enfant)
//   └── Label (TextMeshProUGUI — enfant direct ou sous-enfant)
public class FoundationBuildPanelUI : MonoBehaviour
{
    [Header("Références UI")]
    [SerializeField] private GameObject panelRoot;
    [SerializeField] private Transform buttonsParent;
    [SerializeField] private Button buttonPrefab;

    // Référence au SelectionManager trouvée une seule fois dans Start
    private SelectionManager selectionManager;

    // Foundation actuellement trackée (null si aucune)
    private FoundationComponent currentFoundation;

    // Boutons instanciés dynamiquement — détruits et recréés à chaque nouvelle Foundation
    private List<Button> instantiatedButtons = new List<Button>();

    // Trouve le SelectionManager et s'abonne à l'event Foundation
    private void Start()
    {
        selectionManager = Object.FindFirstObjectByType<SelectionManager>();

        if (selectionManager == null)
        {
            Debug.LogError("🌱 FoundationBuildPanelUI: SelectionManager introuvable dans la scène !");
            return;
        }

        selectionManager.OnFoundationSelected.AddListener(HandleFoundationSelected);

        if (panelRoot != null) panelRoot.SetActive(false);

        Debug.Log("🌱 FoundationBuildPanelUI: Initialisé et abonné à SelectionManager.OnFoundationSelected");
    }

    // Désabonne proprement tous les listeners pour éviter les NullRef
    private void OnDestroy()
    {
        if (selectionManager != null)
        {
            selectionManager.OnFoundationSelected.RemoveListener(HandleFoundationSelected);
        }

        UnsubscribeFromFoundation();
    }

    // === HANDLERS EVENTS ===

    // Appelé par SelectionManager quand un Rootborn local est sélectionné sur une Foundation (ou null)
    private void HandleFoundationSelected(FoundationComponent foundation)
    {
        UnsubscribeFromFoundation();

        if (foundation == null)
        {
            HidePanel();
            return;
        }

        currentFoundation = foundation;
        currentFoundation.OnStateChanged.AddListener(RefreshButtons);
        currentFoundation.OnDestroyed.AddListener(HandleFoundationDestroyed);

        PopulateButtons();
        ShowPanel();
        RefreshButtons();

        Debug.Log($"🌱 FoundationBuildPanelUI: Foundation '{foundation.gameObject.name}' sélectionnée — panneau affiché");
    }

    // Appelé quand la Foundation est détruite (ex: après build réussi)
    // La Foundation est en cours de destruction, on ne tente pas de RemoveListener
    private void HandleFoundationDestroyed()
    {
        currentFoundation = null;
        HidePanel();
        Debug.Log("🌱 FoundationBuildPanelUI: Foundation détruite — panneau masqué");
    }

    // Désabonne et oublie la Foundation courante
    private void UnsubscribeFromFoundation()
    {
        if (currentFoundation == null) return;

        currentFoundation.OnStateChanged.RemoveListener(RefreshButtons);
        currentFoundation.OnDestroyed.RemoveListener(HandleFoundationDestroyed);
        currentFoundation = null;
    }

    // === GESTION DES BOUTONS ===

    // Détruit les boutons existants et crée un bouton par bâtiment disponible
    private void PopulateButtons()
    {
        // Nettoyage des boutons précédents
        foreach (Button button in instantiatedButtons)
        {
            if (button != null) Destroy(button.gameObject);
        }
        instantiatedButtons.Clear();

        if (currentFoundation == null || buttonPrefab == null || buttonsParent == null) return;

        FoundationData data = currentFoundation.GetData();
        if (data == null || data.availableBuildings == null) return;

        foreach (BuildingStatsData building in data.availableBuildings)
        {
            if (building == null) continue;
            if (building.sizeX != 2 || building.sizeY != 2) continue;

            Button button = Instantiate(buttonPrefab, buttonsParent);

            // Portrait : premier enfant Image (pas l'Image du bouton lui-même)
            Image portraitImage = FindChildImage(button);
            if (portraitImage != null && building.portrait != null)
            {
                portraitImage.sprite = building.portrait;
            }

            // Label : premier TextMeshProUGUI dans la hiérarchie du bouton
            TextMeshProUGUI label = button.GetComponentInChildren<TextMeshProUGUI>();
            if (label != null)
            {
                label.text = building.buildingName;
            }

            // Capture de la référence pour la closure du onClick
            BuildingStatsData capturedBuilding = building;
            button.onClick.AddListener(() =>
            {
                if (currentFoundation != null)
                {
                    currentFoundation.RequestBuild(capturedBuilding);
                }
            });

            instantiatedButtons.Add(button);
        }

        Debug.Log($"🌱 FoundationBuildPanelUI: {instantiatedButtons.Count} bouton(s) créé(s)");
    }

    // Met à jour l'interactabilité de tous les boutons selon IsReadyToBuild()
    // Appelé via OnStateChanged de la Foundation (event-driven, jamais dans Update)
    private void RefreshButtons()
    {
        if (currentFoundation == null) return;

        bool canBuild = currentFoundation.IsReadyToBuild();

        foreach (Button button in instantiatedButtons)
        {
            if (button != null)
            {
                button.interactable = canBuild;
            }
        }

        if (debugPanel)
        {
            Debug.Log($"🌱 FoundationBuildPanelUI: RefreshButtons — canBuild={canBuild}, " +
                      $"occupants={currentFoundation.GetCurrentOccupants().Count}/4");
        }
    }

    // === HELPERS VISUELS ===

    // Retourne la première Image enfant d'un bouton qui N'EST PAS sur le GameObject racine du bouton
    // Permet de distinguer l'Image fond du bouton de l'Image portrait
    private Image FindChildImage(Button button)
    {
        foreach (Image image in button.GetComponentsInChildren<Image>())
        {
            if (image.transform != button.transform) return image;
        }
        return null;
    }

    // Affiche le panneau
    private void ShowPanel()
    {
        if (panelRoot != null) panelRoot.SetActive(true);
    }

    // Masque le panneau et vide les boutons existants
    private void HidePanel()
    {
        if (panelRoot != null) panelRoot.SetActive(false);
    }

    [Header("Debug")]
    [SerializeField] private bool debugPanel = false;

    // Affiche l'état courant du panneau dans la console
    [ContextMenu("Debug: Afficher état panneau")]
    private void DebugPanelState()
    {
        Debug.Log($"=== FoundationBuildPanelUI Debug — {gameObject.name} ===");
        Debug.Log($"Foundation trackée : {(currentFoundation != null ? currentFoundation.gameObject.name : "aucune")}");
        Debug.Log($"Panneau actif : {(panelRoot != null && panelRoot.activeSelf)}");
        Debug.Log($"Boutons instanciés : {instantiatedButtons.Count}");

        if (currentFoundation != null)
        {
            Debug.Log($"IsReadyToBuild : {currentFoundation.IsReadyToBuild()}");
        }
    }
}
