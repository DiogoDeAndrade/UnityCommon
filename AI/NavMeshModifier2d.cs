using UnityEngine;
using NaughtyAttributes;
using UnityEngine.Tilemaps;
using System;

namespace UC
{
    public class NavMeshModifier2d : MonoBehaviour
    {
        [System.Serializable]
        struct CostPerTileType
        {
            public TileBase tile;
            [SerializeField]
            public bool     overrideCost;
            [SerializeField, ShowIf(nameof(overrideCost))]
            public float    costMultiplier;
        }   

        [SerializeField]
        private int                 _priority = 0;
        [SerializeField, ShowIf(nameof(isTilemap))]
        private CostPerTileType[]   costPerTileTypes;
        [SerializeField, ShowIf(nameof(isCollider))]
        private bool                overrideCost;
        [SerializeField, ShowIf(nameof(needCostMultiplier))]
        private float               costMultiplier = 1.0f;

        public int priority => _priority;

        bool isTilemap
        {
            get
            {
                if (tilemap) return tilemap;
                tilemap = GetComponent<Tilemap>();
                return tilemap != null;
            }
        }
        bool isCollider
        {
            get
            {
                if (mainCollider) return true;
                mainCollider = GetComponent<Collider2D>();
                return mainCollider != null;
            }
        }
        bool needCostMultiplier => isCollider && overrideCost;

        Tilemap     tilemap;
        Collider2D  mainCollider;

        public bool InfluenceCost(Vector2 pos, ref float cost)
        {
            if ((isTilemap) && (costPerTileTypes != null) && (costPerTileTypes.Length > 0))
            {
                // Get tile at this world position
                var tilePos = tilemap.WorldToCell(pos);
                var tile = tilemap.GetTile(tilePos);

                foreach (var costPerTileType in costPerTileTypes)
                {
                    if ((costPerTileType.tile == tile) && (costPerTileType.overrideCost))
                    {
                        cost = costPerTileType.costMultiplier;
                        return true;
                    }
                }
            }
            if (isCollider)
            {
                bool b = mainCollider.enabled;
                mainCollider.enabled = true;
                if ((overrideCost) && (mainCollider.OverlapPoint(pos)))
                {
                    cost = costMultiplier;
                    mainCollider.enabled = b;
                    return true;
                }
                mainCollider.enabled = b;
            }

            return false;
        }
    }
}
