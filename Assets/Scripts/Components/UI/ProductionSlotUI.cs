using UnityEngine;
using UnityEngine.UI;

// Slot rond représentant une unité ou un bâtiment produisible dans l'ActionPanel
// Configuré dynamiquement via Setup() — pas de création/destruction de GameObjects
// Deux surcharges : UnitStatsData (production/upgrade) et BuildingStatsData (foundation build)
public class ProductionSlotUI : MonoBehaviour
{
    // État de disponibilité d'un slot — déterminé par ActionPanelUI
    public enum SlotState
    {
        Available,          // Ressources suffisantes et conditions remplies
        NotEnoughWatersap,  // Conditions remplies mais ressources insuffisantes
        Locked              // Conditions non remplies (tier verrouillé, foundation pas prête)
    }

    [Header("Références")]
    [Tooltip("Image enfant dans le rond — reçoit le sprite du portrait")]
    [SerializeField] private Image portraitImage;

    [Tooltip("Image du cercle parent — reçoit la couleur d'overlay selon l'état")]
    [SerializeField] private Image slotBackground;

    [Tooltip("Button cliquable sur le slot — interactable selon l'état")]
    [SerializeField] private Button slotButton;

    private static readonly Color ColorOverlayNotEnoughWatersap = new Color(0.1f, 0.3f, 0.8f, 0.45f);
    private static readonly Color ColorPortraitLocked            = new Color(0.4f, 0.4f, 0.4f, 1f);

    // Configure le slot avec une unité, son état et le callback de clic
    public void Setup(UnitStatsData unitData, SlotState state, System.Action<UnitStatsData> onClick)
    {
        if (unitData == null) { gameObject.SetActive(false); return; }

        Debug.Log($"🌿 ProductionSlotUI Setup — état: {state} | slotButton null: {slotButton == null} | portraitImage null: {portraitImage == null}");
        SetupInternal(unitData.portrait, state, onClick != null ? () => onClick(unitData) : null);
    }

    // Configure le slot avec un bâtiment (Foundation build), son état et le callback de clic
    public void Setup(BuildingStatsData buildingData, SlotState state, System.Action<BuildingStatsData> onClick)
    {
        if (buildingData == null) { gameObject.SetActive(false); return; }

        Debug.Log($"🌱 ProductionSlotUI Setup building — état: {state} | {buildingData.buildingName}");
        SetupInternal(buildingData.portrait, state, onClick != null ? () => onClick(buildingData) : null);
    }

    // Logique d'affichage commune aux deux surcharges : portrait, overlay, interactabilité
    private void SetupInternal(Sprite portrait, SlotState state, System.Action onClick)
    {
        gameObject.SetActive(true);

        if (portraitImage != null)
            portraitImage.sprite = portrait;

        slotButton?.onClick.RemoveAllListeners();

        switch (state)
        {
            case SlotState.Available:
                if (portraitImage != null) portraitImage.color  = Color.white;
                if (slotBackground != null) slotBackground.color = Color.white;
                if (slotButton != null)
                {
                    slotButton.interactable = true;
                    if (onClick != null)
                        slotButton.onClick.AddListener(() => onClick());
                }
                break;

            case SlotState.NotEnoughWatersap:
                if (portraitImage != null) portraitImage.color  = Color.white;
                if (slotBackground != null) slotBackground.color = ColorOverlayNotEnoughWatersap;
                if (slotButton != null) slotButton.interactable = false;
                break;

            case SlotState.Locked:
                if (portraitImage != null) portraitImage.color  = ColorPortraitLocked;
                if (slotBackground != null) slotBackground.color = Color.white;
                if (slotButton != null) slotButton.interactable = false;
                break;
        }
    }
}
