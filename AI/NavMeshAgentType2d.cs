using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace UC
{
    [CreateAssetMenu(fileName = "AgentType", menuName = "Unity Common/Data/Agent Type")]
    public class NavMeshAgentType2d : ScriptableObject
    {
        [SerializeField] public NavMeshAgentType2d  parentType;
        [SerializeField] public float               agentRadius = 1.0f;
        [SerializeField] public AgentTerrain        terrainCost = new();

        public float GetCost(NavMeshTerrainType2d terrainType)
        {
            if (terrainCost.ContainsKey(terrainType)) return terrainCost[terrainType];

            return terrainType.defaultCost;
        }
    }

    [System.Serializable]
    public class AgentTerrain : SerializedDictionary<NavMeshTerrainType2d, float>
    {

    }
}
