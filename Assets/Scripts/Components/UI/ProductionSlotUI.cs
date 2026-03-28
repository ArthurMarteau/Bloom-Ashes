using UnityEngine;
using UnityEngine.UI;

// Slot rond représentant une unité produisible dans le ProductionPanel
// Configuré dynamiquement par ProductionPanelUI selon la disponibilité et le Watersap
// Chaque slot est réutilisé via Setup() — pas de création/destruction de GameObjects
public class ProductionSlotUI : MonoBehaviour
{
    // État de disponibilité d'un slot — déterminé par ProductionPanelUI
    public enum SlotState
    {
        Available,          // Watersap suffisant et tier débloqué
        NotEnoughWatersap,  // Tier débloqué mais ressources insuffisantes
        Locked              // Tier verrouillé (recherche non effectuée)
    }

    [Header("Références")]
    [Tooltip("Image enfant dans le rond — reçoit le sprite du portrait")]
    [SerializeField] private Image portraitImage;

    [Tooltip("Image du cercle parent — reçoit la couleur d'overlay selon l'état")]
    [SerializeField] private Image slotBackground;

    [Tooltip("Button cliquable sur le slot — interactable selon l'état")]
    [SerializeField] private Button slotButton;

    // Overlay bleu léger pour l'état NotEnoughWatersap
    private static readonly Color ColorOverlayNotEnoughWatersap = new Color(0.1f, 0.3f, 0.8f, 0.45f);

    // Couleur grise pour le portrait en état Locked (simule le noir et blanc)
    private static readonly Color ColorPortraitLocked = new Color(0.4f, 0.4f, 0.4f, 1f);

    // Configure le slot avec l'unité, l'état et le callback de clic
    // Appelé par ProductionPanelUI.RefreshPanel() à chaque mise à jour de la sélection
    public void Setup(UnitStatsData unitData, SlotState state, System.Action<UnitStatsData> onClick)
    {
        // Unité null : masque le slot entier
        if (unitData == null)
        {
            gameObject.SetActive(false);
            return;
        }

        gameObject.SetActive(true);

        Debug.Log($"🌿 ProductionSlotUI Setup — état: {state} | slotButton null: {slotButton == null} | portraitImage null: {portraitImage == null}");

        // Portrait
        if (portraitImage != null)
            portraitImage.sprite = unitData.portrait;

        // Nettoie les listeners précédents pour éviter les doublons sur les appels successifs
        slotButton?.onClick.RemoveAllListeners();

        switch (state)
        {
            case SlotState.Available:
                // Portrait en couleur normale, pas d'overlay, bouton actif
                if (portraitImage != null)
                    portraitImage.color = Color.white;
                if (slotBackground != null)
                    slotBackground.color = Color.white;
                if (slotButton != null)
                {
                    slotButton.interactable = true;
                    if (onClick != null)
                    {
                        Debug.Log($"🌿 ProductionSlotUI : listener ajouté pour {unitData.unitName}");
                        slotButton.onClick.AddListener(() => onClick(unitData));
                    }
                }
                break;

            case SlotState.NotEnoughWatersap:
                // Portrait normal, overlay bleu sur le fond, bouton désactivé
                if (portraitImage != null)
                    portraitImage.color = Color.white;
                if (slotBackground != null)
                    slotBackground.color = ColorOverlayNotEnoughWatersap;
                if (slotButton != null)
                    slotButton.interactable = false;
                break;

            case SlotState.Locked:
                // Portrait grisé (simule noir et blanc), pas d'overlay, bouton désactivé
                if (portraitImage != null)
                    portraitImage.color = ColorPortraitLocked;
                if (slotBackground != null)
                    slotBackground.color = Color.white;
                if (slotButton != null)
                    slotButton.interactable = false;
                break;
        }
    }
}
