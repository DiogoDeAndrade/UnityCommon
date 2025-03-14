using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public class GridAction_Tile : GridAction
{
    [SerializeField] private List<TileBase>     tiles;
    [SerializeField] private GridAction[]       actions;

    Tilemap tilemap;

    void Awake()
    {
        tilemap = GetComponent<Tilemap>();
    }

    public override void GatherActions(GridObject subject, Vector2Int position, List<GridAction> retActions)
    {
        var tile = tilemap.GetTile(position.xy0());
        if (tiles.Contains(tile))
        {
            foreach (var action in actions)
            {
                action.GatherActions(subject, position, retActions);
            }
        }
    }

    public override bool RunAction(GridObject subject, Vector2Int position)
    {
        throw new System.NotImplementedException();
    }
}
