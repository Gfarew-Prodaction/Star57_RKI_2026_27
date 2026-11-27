using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Zvezda57.UI
{
    [Serializable]
    internal sealed class LabeledButton
    {
        [SerializeField]
        private Button button;
        [SerializeField]
        private AuthoredText caption;

        public void Bind(UnityAction action)
        {
            button.onClick.AddListener(action);
        }

        public void Show(bool visible)
        {
            button.gameObject.SetActive(visible);
        }

        public void SetCaption(string value)
        {
            caption.SetContent(value);
        }

        public bool Interactable
        {
            set => button.interactable = value;
        }
    }
}
