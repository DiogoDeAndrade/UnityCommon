using UnityEngine;

namespace UC
{

    public class UIDiscreteCounter : MonoBehaviour
    {
        [SerializeField] private GameObject[] targetObjects;

        int count = 0;

        void UpdateUI()
        {
            for (int i = 0; i < Mathf.Min(targetObjects.Length, count); i++)
            {
                targetObjects[i].SetActive(true);
            }
            for (int i = Mathf.Max(0, Mathf.Min(targetObjects.Length, count)); i < targetObjects.Length; i++)
            {
                targetObjects[i].SetActive(false);
            }
        }

        public void SetCount(int count)
        {
            this.count = count;

            UpdateUI();
        }

        public int GetCount() => count;
    }
}