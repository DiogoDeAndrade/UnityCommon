using UnityEngine;
using NaughtyAttributes;
using UnityEngine.Tilemaps;
using System;

namespace UC
{
    public class NavMeshModifier2d : MonoBehaviour
    {
        [System.Serializable]
        struct DataPerTileType
        {
            public TileBase             tile;
            [SerializeField]
            public bool                 overrideTerrainType;
            [SerializeField, ShowIf(nameof(overrideTerrainType))]
            public NavMeshTerrainType2d terrainType;
        }   

        [SerializeField]
        private int                     _priority = 0;
        [SerializeField, ShowIf(nameof(isTilemap))]
        private DataPerTileType[]       dataPerTileTypes;
        [SerializeField, ShowIf(nameof(isCollider))]
        private bool                    overrideTerrainType;
        [SerializeField, ShowIf(nameof(needTerrainType))]
        private NavMeshTerrainType2d    terrainType;

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
        bool needTerrainType => isCollider && overrideTerrainType;

        Tilemap     tilemap;
        Collider2D  mainCollider;

        public bool InfluenceTerrainType(Vector2 pos, ref NavMeshTerrainType2d terrainType)
        {
            if ((isTilemap) && (dataPerTileTypes != null) && (dataPerTileTypes.Length > 0))
            {
                // Get tile at this world position
                var tilePos = tilemap.WorldToCell(pos);
                var tile = tilemap.GetTile(tilePos);

                foreach (var costPerTileType in dataPerTileTypes)
                {
                    if ((costPerTileType.tile == tile) && (costPerTileType.overrideTerrainType))
                    {
                        terrainType = costPerTileType.terrainType;
                        return true;
                    }
                }
            }
            if (isCollider)
            {
                bool b = mainCollider.enabled;
                mainCollider.enabled = true;
                if ((overrideTerrainType) && (mainCollider.OverlapPoint(pos)))
                {
                    terrainType = this.terrainType;
                    mainCollider.enabled = b;
                    return true;
                }
                mainCollider.enabled = b;
            }

            return false;
        }
    }
}
