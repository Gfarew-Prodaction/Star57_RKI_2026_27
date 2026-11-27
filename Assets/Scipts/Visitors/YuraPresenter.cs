using TMPro;
using UnityEngine;
using Zvezda57.InputSystem;
using Zvezda57.Systems;

namespace Zvezda57.Visitors
{
    public sealed class YuraPresenter : MonoBehaviour
    {
        [SerializeField]
        private Transform visualHead, placeholderHead;
        [SerializeField]
        private WorldAction body;
        [SerializeField]
        private TMP_Text label;
        private IYuraAssignments _campaign;
        private Transform _head;
        private Quaternion _rest;
        public void Build(IYuraAssignments campaign)
        {
            _campaign = campaign;
            _head = visualHead != null ? visualHead : placeholderHead;
            body.Configure("Юра: поручить проверку архива", () => campaign.Assign(YuraTask.Archive, out _));
            _rest = _head.localRotation;
            campaign.Changed += Refresh;
            Refresh();
        }

        private void Refresh()
        {
            label.text = _campaign.Task == YuraTask.None ? "Юра" : "Юра · " + Mathf.CeilToInt(_campaign.TaskRemaining) + " с";
            _head.localRotation = _rest * Quaternion.Euler(_campaign.Task == YuraTask.None ? 0 : 15, 0, 0);
        }

        private void OnDestroy()
        {
            if (_campaign != null)
            {
                _campaign.Changed -= Refresh;
            }
        }
    }
}
