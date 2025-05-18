using UnityEngine;
using UnityEngine.Rendering.Universal;

public class UnitHaloEffect : MonoBehaviour
{
    private Light2D haloLight;

    void Start()
    {
        // Trouve le composant Light attaché à ce GameObject
        haloLight = GetComponent<Light2D>();

        // Désactive la lumière au démarrage
        if (haloLight != null)
        {
            haloLight.enabled = false;
        }
    }

    public void ActivateHalo()
    {
        // Active la lumière
        if (haloLight != null)
        {
            haloLight.enabled = true;
        }
    }

    public void DeactivateHalo()
    {
        // Désactive la lumière
        if (haloLight != null)
        {
            haloLight.enabled = false;
        }
    }
}

