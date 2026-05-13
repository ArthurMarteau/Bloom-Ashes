using UnityEngine;

[CreateAssetMenu(fileName = "New Cursor Data", menuName = "Bloom and Ashes/Data/UI/Cursor Data")]
public class CursorData : ScriptableObject
{
    [Header("Cursor")]
    [Tooltip("Texture du curseur. Dans Unity, importer la texture en mode Cursor.")]
    public Texture2D cursorTexture;

    [Tooltip("Point actif du curseur en pixels depuis le coin haut-gauche.")]
    public Vector2 hotspot = Vector2.zero;

    [Tooltip("Mode de rendu du curseur Unity.")]
    public CursorMode cursorMode = CursorMode.Auto;
}
