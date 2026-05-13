using UnityEngine;

public class ButtonDebugTest : MonoBehaviour
{
    public void OnButtonClicked()
    {
        Debug.Log($"✅ ButtonDebugTest : clic reçu sur {gameObject.name}");
    }
}