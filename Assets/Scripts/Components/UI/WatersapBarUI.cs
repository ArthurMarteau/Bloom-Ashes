using UnityEngine;
using UnityEngine.UI;
using TMPro;

// Barre de watersap du joueur local — Image Filled Horizontal, couleur #04C7C4
// S'abonne à WatersapManager.OnWatersapChanged et filtre par trackedTeamID
// Délègue le calcul du ratio à WatersapSystem (POCO pur)
//
// SETUP PREFAB — Hiérarchie attendue :
//   Canvas
//   └── WatersapBar
//       ├── WatersapBarFill (Image, fillMethod = Horizontal, fillOrigin = Left)
//       └── WatersapText (TextMeshProUGUI — "240 / 300")
public class WatersapBarUI : MonoBehaviour
{
    [Header("Barre de watersap")]
    [SerializeField] private Image watersapBarFill;         // Image Filled Horizontal
    [SerializeField] private TextMeshProUGUI watersapText;  // Affiche "240 / 300"

    [Header("Configuration multijoueur")]
    [SerializeField] private int trackedTeamID = 1; // Équipe du joueur local — à remplacer par l'ID réseau

    // Couleur de la barre : #04C7C4
    private static readonly Color watersapColor = new Color(4f / 255f, 199f / 255f, 196f / 255f, 1f);

    // Système de calcul du ratio — instancié localement, pas de dépendance singleton
    private WatersapSystem watersapSystem = new WatersapSystem();

    // Applique la couleur fixe sur la barre dès le démarrage
    private void Awake()
    {
        if (watersapBarFill != null)
            watersapBarFill.color = watersapColor;
    }

    // Abonne l'UI à l'événement watersap et initialise l'affichage
    // Résout l'équipe suivie depuis PlayerManager si disponible (sinon fallback sur trackedTeamID)
    private void Start()
    {
        if (PlayerManager.Instance != null)
            trackedTeamID = PlayerManager.Instance.LocalPlayer.teamID;

        if (WatersapManager.Instance == null)
        {
            GameLogger.Error("💧 WatersapBarUI : WatersapManager introuvable dans la scène !", this);
            return;
        }

        WatersapManager.Instance.OnWatersapChanged.AddListener(OnWatersapChanged);

        // Initialise l'affichage avec la valeur actuelle de l'équipe suivie
        int current = WatersapManager.Instance.GetWatersap(trackedTeamID);
        RefreshDisplay(current);
    }

    // Désabonnement propre pour éviter les références mortes
    private void OnDestroy()
    {
        if (WatersapManager.Instance != null)
            WatersapManager.Instance.OnWatersapChanged.RemoveListener(OnWatersapChanged);
    }

    // Reçoit l'événement — ignore les équipes autres que trackedTeamID
    private void OnWatersapChanged(int teamID, int newAmount)
    {
        if (teamID != trackedTeamID) return;
        RefreshDisplay(newAmount);
    }

    // Met à jour le fillAmount et le texte "current / max"
    private void RefreshDisplay(int current)
    {
        if (WatersapManager.Instance == null) return;

        int max = WatersapManager.Instance.MaxWatersap;

        if (watersapBarFill != null)
            watersapBarFill.fillAmount = watersapSystem.CalculateFillRatio(current, max);

        if (watersapText != null)
            watersapText.text = $"{current} / {max}";

        GameLogger.Log(GameLogger.Category.Watersap, $"💧 WatersapBarUI : équipe {trackedTeamID} → {current} / {max}", this);
    }
}
