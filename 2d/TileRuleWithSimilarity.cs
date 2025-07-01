using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

#if UNITY_2D_TILES_AVAILABLE

namespace UC
{

    [CreateAssetMenu(fileName = "Tile Rule with Similarity", menuName = "Unity Common/Tiles/Rule With Similarity")]
    public class TileRuleWithSimilarity : RuleTile<RuleTile.TilingRule.Neighbor>
    {
        [SerializeField] List<Sprite> spritesInFamily;

        public override bool RuleMatch(int neighbor, TileBase tile)
        {
            if (tile == null)
            {
                return neighbor != TilingRule.Neighbor.This;
            }

            // If the tile is a standard Tile, check its sprite
            if (tile is UnityEngine.Tilemaps.Tile tileAsset)
            {
                if (spritesInFamily.Contains(tileAsset.sprite))
                {
                    // Treat it as if it's part of this rule-based tile family
                    return (neighbor == TilingRule.Neighbor.This);
                }
                else
                {
                    return (neighbor == TilingRule.Neighbor.NotThis);
                }
            }

            // Check normal RuleTile behavior for any remaining cases
            return base.RuleMatch(neighbor, tile);
        }
    }
}

#endif