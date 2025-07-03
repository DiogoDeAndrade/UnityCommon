using UnityEngine;
using UnityEngine.UI;

public class DisplayGlobalRT : MonoBehaviour
{
    [SerializeField] private string globalTextureName;
    
    Texture texture;

    void Update()
    {
        if (texture == null)
        {
            texture = Shader.GetGlobalTexture(globalTextureName);
            if (texture != null)
            {
                RawImage ri = GetComponent<RawImage>();
                if (ri)
                {
                    ri.texture = texture;
                }
            }
        }
    }
}
