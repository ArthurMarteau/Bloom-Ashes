using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

// Slot rond représentant une unité produisible dans l'ActionPanel
// Configuré dynamiquement par ActionPanelUI selon la disponibilité et le Watersap
// Chaque slot est réutilisé via Setup() — pas de création/destruction de GameObjects
public class ProductionSlotUI : MonoBehaviour
{
    // État de disponibilité d'un slot — déterminé par ActionPanelUI
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

    [SerializeField] private TooltipTrigger tooltipTrigger;

    private UnityAction buttonClickListener;

    // Overlay bleu léger pour l'état NotEnoughWatersap
    private static readonly Color ColorOverlayNotEnoughWatersap = new Color(0.1f, 0.3f, 0.8f, 0.45f);

    // Couleur grise pour le portrait en état Locked (simule le noir et blanc)
    private static readonly Color ColorPortraitLocked = new Color(0.4f, 0.4f, 0.4f, 1f);

    // Configure le slot avec l'unité, l'état et le callback de clic
    // Appelé par ActionPanelUI lors de chaque mise à jour de la sélection
    public void Setup(UnitStatsData unitData, SlotState state, System.Action<UnitStatsData> onClick, int tooltipCost = -1)
    {
        // Unité null : masque le slot entier
        if (unitData == null)
        {
            gameObject.SetActive(false);
            return;
        }

        gameObject.SetActive(true);

        int watersapCost = tooltipCost == -1 ? unitData.watersapCost : tooltipCost;
        string title = unitData.unitName;
        string body = $"{unitData.description}\n<color=#007A8A>Coût : {watersapCost} Watersap</color>";
        tooltipTrigger?.SetContent(title, body);

        GameLogger.Log(GameLogger.Category.UI, $"🌿 ProductionSlotUI Setup — état: {state} | slotButton null: {slotButton == null} | portraitImage null: {portraitImage == null}", this);

        // Portrait
        if (portraitImage != null)
            portraitImage.sprite = unitData.portrait;

        // Nettoie le listener dynamique précédent pour éviter les doublons.
        RemoveButtonListener();

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
                        GameLogger.Log(GameLogger.Category.UI, $"🌿 ProductionSlotUI : listener ajouté pour {unitData.unitName}", this);
                        buttonClickListener = () => onClick(unitData);
                        slotButton.onClick.AddListener(buttonClickListener);
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

    // Configure le slot avec un bâtiment constructible depuis une Foundation
    public void Setup(BuildingStatsData buildingData, SlotState state, System.Action<BuildingStatsData> onClick)
    {
        if (buildingData == null)
        {
            gameObject.SetActive(false);
            return;
        }

        gameObject.SetActive(true);

        string title = buildingData.buildingName;
        string body = $"{buildingData.description}\n<color=#007A8A>Construction gratuite</color>";
        tooltipTrigger?.SetContent(title, body);

        GameLogger.Log(GameLogger.Category.UI, $"🌿 ProductionSlotUI Setup bâtiment — état: {state} | slotButton null: {slotButton == null} | portraitImage null: {portraitImage == null}", this);

        if (portraitImage != null)
            portraitImage.sprite = buildingData.portrait;

        RemoveButtonListener();

        switch (state)
        {
            case SlotState.Available:
                if (portraitImage != null)
                    portraitImage.color = Color.white;
                if (slotBackground != null)
                    slotBackground.color = Color.white;
                if (slotButton != null)
                {
                    slotButton.interactable = true;
                    if (onClick != null)
                    {
                        buttonClickListener = () => onClick(buildingData);
                        slotButton.onClick.AddListener(buttonClickListener);
                    }
                }
                break;

            case SlotState.NotEnoughWatersap:
                if (portraitImage != null)
                    portraitImage.color = Color.white;
                if (slotBackground != null)
                    slotBackground.color = ColorOverlayNotEnoughWatersap;
                if (slotButton != null)
                    slotButton.interactable = false;
                break;

            case SlotState.Locked:
                if (portraitImage != null)
                    portraitImage.color = ColorPortraitLocked;
                if (slotBackground != null)
                    slotBackground.color = Color.white;
                if (slotButton != null)
                    slotButton.interactable = false;
                break;
        }
    }

    // Retire uniquement le listener dynamique installé par ce slot.
    private void RemoveButtonListener()
    {
        if (slotButton != null && buttonClickListener != null)
            slotButton.onClick.RemoveListener(buttonClickListener);

        buttonClickListener = null;
    }

    // Nettoie le listener dynamique lorsque le slot est détruit.
    private void OnDestroy()
    {
        RemoveButtonListener();
    }
}
