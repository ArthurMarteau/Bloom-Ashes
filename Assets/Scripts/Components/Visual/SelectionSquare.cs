using UnityEngine;

// Composant qui gère l'affichage du carré de sélection
// S'attache dynamiquement à l'objet sélectionné ou à une position de grille
// Se détruit automatiquement quand l'objet parent est détruit
public class SelectionSquare : MonoBehaviour
{
    [Header("Selection Square Settings")]
    [SerializeField] private SpriteRenderer spriteRenderer;

    // Référence vers l'objet suivi (peut être null pour ground)
    private GameObject targetObject;

    // Position fixe sur grille (utilisée pour ground uniquement)
    private Vector2Int gridPosition;

    // Mode de suivi : GameObject ou position fixe
    private bool followGameObject;

    // Méthode appelée au démarrage pour initialiser les composants
    private void Awake()
    {
        // Récupère le SpriteRenderer automatiquement si pas assigné
        if (spriteRenderer == null)
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
        }

        // Vérifie que le SpriteRenderer existe
        if (spriteRenderer == null)
        {
            GameLogger.Error("SelectionSquare nécessite un SpriteRenderer !", this);
        }
    }

    // Méthode appelée chaque frame pour gérer le suivi d'objet
    private void Update()
    {
        // Si on suit un GameObject, vérifie qu'il existe encore et le suit
        if (followGameObject)
        {
            if (CheckTargetDestroyed())
            {
                return; // L'objet s'est auto-détruit
            }

            FollowTargetObject();
        }
    }

    // Attache le carré de sélection à un GameObject spécifique
    // Utilisé pour les unités et obstacles qui peuvent bouger
    public void AttachToGameObject(GameObject target)
    {
        if (target == null)
        {
            GameLogger.Warn(GameLogger.Category.Selection, "Tentative d'attacher SelectionSquare à un objet null !", this);
            return;
        }

        // Configure le mode GameObject
        targetObject = target;
        followGameObject = true;

        // Place immédiatement le carré sur l'objet
        transform.position = target.transform.position;

        GameLogger.Log(GameLogger.Category.Selection, $"SelectionSquare attaché à {target.name}", this);
    }

    // Attache le carré de sélection à une position fixe de grille
    // Utilisé pour la sélection du terrain (ground)
    public void AttachToGridPosition(Vector2Int gridPos)
    {
        // Vérifie que la position est valide
        if (!GridManager.Instance.IsInBounds(gridPos))
        {
            GameLogger.Warn(GameLogger.Category.Selection, $"Position de grille invalide : {gridPos}", this);
            return;
        }

        // Configure le mode position fixe
        gridPosition = gridPos;
        followGameObject = false;
        targetObject = null;

        // Place le carré au centre de la cellule
        Vector3 worldPosition = GridManager.Instance.GridToWorld(gridPos);
        transform.position = worldPosition;

        GameLogger.Log(GameLogger.Category.Selection, $"SelectionSquare placé sur la cellule {gridPos}", this);
    }

    // Suit automatiquement la position de l'objet cible
    // Appelé chaque frame si followGameObject = true
    private void FollowTargetObject()
    {
        if (targetObject != null)
        {
            // Met à jour la position pour suivre l'objet
            transform.position = targetObject.transform.position;
        }
    }

    // Vérifie si l'objet cible a été détruit et se détruit si nécessaire
    // Retourne true si l'objet s'est auto-détruit
    private bool CheckTargetDestroyed()
    {
        if (targetObject == null)
        {
            GameLogger.Log(GameLogger.Category.Selection, "Objet cible détruit, destruction du SelectionSquare", this);
            DestroySelectionSquare();
            return true;
        }

        return false;
    }

    // Détruit proprement le carré de sélection
    // Méthode publique pour permettre la destruction externe
    public void DestroySelectionSquare()
    {
        GameLogger.Log(GameLogger.Category.Selection, "Destruction du SelectionSquare", this);
        Destroy(gameObject);
    }

    // Getter pour savoir si le carré suit un objet ou est en position fixe
    // Utile pour debug et vérifications externes
    public bool IsFollowingGameObject()
    {
        return followGameObject;
    }

    // Getter pour récupérer l'objet suivi
    // Retourne null si pas d'objet suivi ou position fixe
    public GameObject GetTargetObject()
    {
        return targetObject;
    }

    // Getter pour récupérer la position de grille
    // Utile pour les sélections de terrain
    public Vector2Int GetGridPosition()
    {
        return gridPosition;
    }
}