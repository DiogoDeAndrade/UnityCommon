using UnityEngine;
using System;

namespace UC
{

    [Serializable]
    public class GridActionCondition
    {
        public enum ConditionType { ResourceValue, Expression, ItemCount };
        public enum Comparison { Less, LessEqual, Greater, GreaterEqual, Equal, NotEqual };

        [SerializeField]
        private ConditionType conditionType;
        [SerializeField]
        private Hypertag targetTag;
        [SerializeField]
        private ResourceType resourceType;
        [SerializeField]
        private Comparison comparison;
        [SerializeField]
        private float refValue;
        [SerializeField]
        private string expression;
        [SerializeField]
        private Item item;
        [SerializeField]
        private int itemQuantity = 1;

        public bool CheckCondition(Expression.IContext context)
        {
            switch (conditionType)
            {
                case ConditionType.ResourceValue:
                    {
                        var obj = Hypertag.FindFirstObjectWithHypertag<Transform>(targetTag);
                        if (obj == null) return false;

                        var resHandler = obj.FindResourceHandler(resourceType);
                        if (resHandler == null) return false;

                        float value = resHandler.resource;

                        switch (comparison)
                        {
                            case Comparison.Less:
                                return value < refValue;
                            case Comparison.LessEqual:
                                return value <= refValue;
                            case Comparison.Greater:
                                return value > refValue;
                            case Comparison.GreaterEqual:
                                return value >= refValue;
                            case Comparison.Equal:
                                return value == refValue;
                            case Comparison.NotEqual:
                                return value != refValue;
                        }
                    }
                    break;
                case ConditionType.Expression:
                    {
                        if (Expression.TryParse(expression, out var parsedExpression))
                        {
                            return parsedExpression.EvaluateBool(context);
                        }
                        else
                        {
                            Debug.LogWarning($"Failed to parse expression {expression}!");
                        }
                    }
                    break;
                case ConditionType.ItemCount:
                    {
                        var obj = Hypertag.FindFirstObjectWithHypertag<Transform>(targetTag);
                        if (obj == null) return false;

                        var inventory = obj.GetComponent<Inventory>();
                        if (inventory == null) return false;

                        int count = inventory.GetItemCount(item);

                        switch (comparison)
                        {
                            case Comparison.Less:
                                return (count < itemQuantity);
                            case Comparison.LessEqual:
                                return (count <= itemQuantity);
                            case Comparison.Greater:
                                return (count > itemQuantity);
                            case Comparison.GreaterEqual:
                                return (count >= itemQuantity);
                            case Comparison.Equal:
                                return (count == itemQuantity);
                            case Comparison.NotEqual:
                                return (count != itemQuantity);
                        }

                        return false;
                    }
                default:
                    break;
            }

            return false;
        }

        public (Item item, int count) GetItem()
        {
            if (conditionType == ConditionType.ItemCount)
            {
                return (item, itemQuantity);
            }

            return (null, 0);
        }

        public Inventory GetInventory()
        {
            var obj = Hypertag.FindFirstObjectWithHypertag<Transform>(targetTag);
            if (obj == null) return null;

            var inventory = obj.GetComponent<Inventory>();

            return inventory;
        }
    }
}