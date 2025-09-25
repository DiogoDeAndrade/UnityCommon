using UnityEngine;

public class ObjectOnlyForBaking : MonoBehaviour
{
    void Start()
    {
        // This object is deleted as soon as the scene loads - it's only for use at bake time.
        Destroy(gameObject);
    }

    void Update()
    {
        
    }
}
