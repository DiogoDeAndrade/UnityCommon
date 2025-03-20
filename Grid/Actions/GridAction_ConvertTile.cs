using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public class GridAction_ConvertTile : GridAction
{
    [SerializeField] private List<TileBase> tiles;
    [SerializeField] private TileBase       convertTo;
    [SerializeField] private GameObject     spawn;

    Tilemap tilemap;

    void Awake()
    {
        tilemap = GetComponent<Tilemap>();
    }

    protected override void ActualGatherActions(GridObject subject, Vector2Int position, List<GridAction> retActions)
    {
        var tile = tilemap.GetTile(position.xy0());
        if (tiles.Contains(tile))
        {
            retActions.Add(this);
        }
    }    

    protected override bool ActualRunAction(GridObject subject, Vector2Int position)
    {
        tilemap.SetTile(position.xy0(), convertTo);

        if (spawn)
        {
            var newObject = Instantiate(spawn, gridSystem.transform);
            newObject.transform.position = gridSystem.GridToWorld(position);
        }

        return true;
    }
}
