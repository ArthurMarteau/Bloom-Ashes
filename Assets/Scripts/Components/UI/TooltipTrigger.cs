using UnityEngine;
using UnityEngine.EventSystems;

// Déclencheur générique de tooltip pour tout élément UI survolable.
public class TooltipTrigger : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] private string tooltipTitle;
    [SerializeField] private string tooltipBody;

    // Met à jour le contenu affiché au prochain survol.
    public void SetContent(string title, string body)
    {
        tooltipTitle = title;
        tooltipBody = body;
    }

    // Affiche le tooltip quand le pointeur entre sur l'élément.
    public void OnPointerEnter(PointerEventData eventData)
    {
        TooltipUI.Instance?.Show(tooltipTitle, tooltipBody);
    }

    // Masque le tooltip quand le pointeur quitte l'élément.
    public void OnPointerExit(PointerEventData eventData)
    {
        TooltipUI.Instance?.Hide();
    }

    // Masque le tooltip si l'élément disparaît pendant le survol.
    private void OnDisable()
    {
        TooltipUI.Instance?.Hide();
    }
}
