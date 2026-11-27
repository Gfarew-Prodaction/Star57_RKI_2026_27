using System;
using UnityEngine;
using Zvezda57.CameraSystem;

namespace Zvezda57.InputSystem
{
    public sealed class WorldAction : MonoBehaviour, IWorldAction
    {
        [SerializeField]
        private Renderer highlightRenderer;
        private Action _action;
        private Renderer _target;
        private Color _original;
        private CameraMode? _required;
        public string Hint { get; private set; }

        public void Configure(string hint, Action callback, CameraMode? mode = null)
        {
            Hint = hint;
            _action = callback;
            _required = mode;
            _target = highlightRenderer != null ? highlightRenderer : GetComponent<Renderer>();
            if (_target != null)
            {
                _original = _target.sharedMaterial.color;
            }
        }

        public bool CanUse(CameraMode mode) => !_required.HasValue || mode == _required;
        public void Use()
        {
            _action?.Invoke();
        }

        public void Hover(bool active)
        {
            if (_target != null)
            {
                _target.material.color = active ? Color.Lerp(_original, Color.white, .3f) : _original;
            }
        }
    }
}
