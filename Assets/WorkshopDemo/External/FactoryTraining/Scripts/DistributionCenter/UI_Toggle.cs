using UnityEngine;
using UnityEngine.UI;

namespace DistributionCenter
{
    public class UI_Toggle : MonoBehaviour
    {
        public GameObject[] objectsToToggle;
        public Button toggleButton;

        public bool startEnabled;
        private bool isEnabled = false;

        void Start()
        {
            SetVisible(startEnabled);

            if (toggleButton != null)
                toggleButton.onClick.AddListener(ToggleObjects);
        }

        public void ToggleObjects()
        {
            SetVisible(!isEnabled);
        }

        public void SetVisible(bool visible)
        {
            isEnabled = visible;
            if (objectsToToggle == null) return;
            foreach (var obj in objectsToToggle)
                if (obj != null)
                    obj.SetActive(isEnabled);
        }
    }
}
