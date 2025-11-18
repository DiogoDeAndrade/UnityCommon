using UC.RPG;
using UnityEngine;

namespace UC
{

    [CreateAssetMenu(fileName = "Weapon", menuName = "Unity Common/Data/Weapon")]
    public partial class Weapon : Item
    {
        [Header("Weapon")]
        [SerializeReference] public AttackModule attackModule;
    }

    [System.Serializable]
    public abstract class AttackModule
    {
        public abstract bool Attack(RPGEntity source, Vector2Int destPos);
    }
}
