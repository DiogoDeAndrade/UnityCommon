using System;
using UC.RPG;
using UnityEngine;

namespace UC.RPG
{

    [Serializable]
    [PolymorphicName("Item/Equipped Items Stat")]
    public class RVFEquippedStatOp : ResourceValueFunction
    {
        public enum Operation { Add, Multiply, Average, Highest, Lowest };

        public Operation    operation = Operation.Add;
        public StatType     stat;

        public override Vector2 GetMinMax(RPGEntity character)
        {
            return new Vector2(0.0f, float.MaxValue);
        }

        public override float GetValue(RPGEntity character)
        {
            float ret = 0.0f;
            if (operation == Operation.Highest) ret = -float.MaxValue;
            else if (operation == Operation.Lowest) ret = float.MaxValue;
            float count = 0.0f;

            var equipment = character.equipment;
            if (equipment != null)
            {
                foreach (var e in equipment)
                {
                    if (e.item == null) continue;
                    var itemStat = e.item.Get(stat);
                    if (itemStat != null)
                    {
                        switch (operation)
                        {
                            case Operation.Add:
                                ret += itemStat.GetValue();
                                break;
                            case Operation.Multiply:
                                ret *= itemStat.GetValue();
                                break;
                            case Operation.Average:
                                ret += itemStat.GetValue();
                                count += 1.0f;
                                break;
                            case Operation.Highest:
                                ret = Mathf.Max(ret, itemStat.GetValue());
                                break;
                            case Operation.Lowest:
                                ret = Mathf.Min(ret, itemStat.GetValue());
                                break;
                            default:
                                break;
                        }
                    }
                }
                if (operation == Operation.Average && count > 0.0f)
                {
                    ret /= count;
                }
            }

            return ret;
        }
    }
}