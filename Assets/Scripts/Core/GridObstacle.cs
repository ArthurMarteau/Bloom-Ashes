using UnityEngine;

public class GridObstacle : MonoBehaviour
{
    public enum ObstacleType
    {
        Solid,
        Water
    }
    [SerializeField] public ObstacleType obstacleType = ObstacleType.Solid;

    private void OnEnable()
    {
        if (GridManager.Instance != null)
            Register();
    }

    private void Start()
    {
        if (GridManager.Instance != null)
            Register();
    }

    private void OnDisable()
    {
        if (GridManager.Instance != null)
            Unregister();
    }

    private void Register()
    {
        var gridPos = GridManager.Instance.WorldToGrid(transform.position);
        GridManager.Instance.RegisterBlockedCell(gridPos, this);
    }

    private void Unregister()
    {
        var gridPos = GridManager.Instance.WorldToGrid(transform.position);
        GridManager.Instance.UnregisterBlockedCell(gridPos);
    }
}

