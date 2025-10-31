using System;
using UnityEngine;

[AttributeUsage(AttributeTargets.Field)]
public class RequireInterfaceAttribute : PropertyAttribute
{
    public Type RequiredType { get; }
    public RequireInterfaceAttribute(Type requiredType) => RequiredType = requiredType;
}
