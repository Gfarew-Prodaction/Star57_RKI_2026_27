using TMPro;
using UnityEngine;

namespace Zvezda57.UI
{
    public sealed class AuthoredText : MonoBehaviour
    {
        [Tooltip("Keep the TMP text entered in the scene instead of updating it from game content. Use for static captions only.")]
        public bool OverrideContent;
        public void SetContent(string value)
        {
            if (!OverrideContent)
            {
                GetComponent<TMP_Text>().text = value;
            }
        }
    }
}
