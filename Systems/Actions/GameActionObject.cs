using UnityEngine;

public class GameActionObject : MonoBehaviour, IGameActionObject
{
    public GameObject GetTargetGameObject() => gameObject;

}
