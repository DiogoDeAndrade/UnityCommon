using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public class GridAction_ConvertTile : GridActionContainer
{
    [SerializeField, Header("Convert Tile")] private List<TileBase> tiles;
    [SerializeField] private TileBase       convertTo;
    [SerializeField] private GameObject     spawn;

    Tilemap tilemap;

    void Awake()
    {
        tilemap = GetComponent<Tilemap>();
    }

    public override void ActualGatherActions(GridObject subject, Vector2Int position, List<NamedAction> retActions)
    {
        var tile = tilemap.GetTile(position.xy0());
        if (tiles.Contains(tile))
        {
            retActions.Add(new NamedAction
            {
                name = verb,
                action = RunAction,
                container = this
            });
        }
    }    

    protected bool RunAction(GridObject subject, Vector2Int position)
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
