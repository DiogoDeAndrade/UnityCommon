using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public class GridAction_Tile : GridActionContainer
{
    [SerializeField] private List<TileBase>     tiles;
    [SerializeField] private GridActionContainer[]       actions;

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
            foreach (var action in actions)
            {
                action.GatherActions(subject, position, retActions);
            }
        }
    }
}
