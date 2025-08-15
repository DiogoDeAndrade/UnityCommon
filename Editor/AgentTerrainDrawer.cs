using UnityEditor;

namespace UC
{
    [CustomPropertyDrawer(typeof(AgentTerrain))]
    public class AgentTerrainDrawer : SerializedDictionaryEditor<NavMeshTerrainType2d, float>
    {
    }
}
