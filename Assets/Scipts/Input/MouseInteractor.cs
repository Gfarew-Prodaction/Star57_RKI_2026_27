using System;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine;
using Zvezda57.CameraSystem;

namespace Zvezda57.InputSystem
{
    public sealed class MouseInteractor : MonoBehaviour
    {
        private Camera _cameraSource;
        private CameraRig _rig;
        private IWorldAction _hovered;
        private IWorldAction _pressed;
        private IWorldDrag _dragged;
        private Vector2 _pressPosition;
        public bool Blocked { get; set; }

        public event Action<string> HintChanged;
        public event Action PauseRequested;
        public void Configure(Camera source, CameraRig cameraRig)
        {
            _cameraSource = source;
            _rig = cameraRig;
        }

        private void Update()
        {
            var mouse = Mouse.current;
            if (mouse == null || _cameraSource == null)
            {
                return;
            }

            var position = mouse.position.ReadValue();
            var ray = _cameraSource.ScreenPointToRay(position);
            bool ui = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
            IWorldAction next = null;
            if (!Blocked && !ui && !_rig.IsMoving && Physics.Raycast(ray, out var hit, 100f))
            {
                next = hit.collider.GetComponent<IWorldAction>();
                if (next != null && !next.CanUse(_rig.Mode))
                {
                    next = null;
                }
            }

            if (!ReferenceEquals(next, _hovered))
            {
                _hovered?.Hover(false);
                _hovered = next;
                _hovered?.Hover(true);
                HintChanged?.Invoke(_hovered?.Hint ?? "");
            }

            if (mouse.leftButton.wasPressedThisFrame)
            {
                _pressed = _hovered;
                _pressPosition = position;
            }

            if (mouse.leftButton.isPressed && !Blocked && !ui && !_rig.IsMoving && _dragged == null && _pressed is IWorldDrag dragTarget && Vector2.Distance(position, _pressPosition) > 8f)
            {
                _dragged = dragTarget;
                _dragged.Begin(_cameraSource.ScreenPointToRay(_pressPosition));
            }

            if (_dragged != null)
            {
                _dragged.Drag(ray);
            }

            if (mouse.leftButton.wasReleasedThisFrame)
            {
                if (_dragged != null)
                {
                    _dragged.End(ray, Blocked || ui || _rig.IsMoving);
                    _dragged = null;
                }
                else if (!Blocked && !_rig.IsMoving && ReferenceEquals(_pressed, _hovered))
                {
                    _pressed?.Use();
                }

                _pressed = null;
            }

            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                PauseRequested?.Invoke();
            }
        }

        public void CancelDrag()
        {
            if (_dragged != null)
            {
                _dragged.End(default, true);
            }

            _dragged = null;
            _pressed = null;
        }

        private void OnDisable()
        {
            CancelDrag();
            _hovered?.Hover(false);
            _hovered = null;
        }
    }
}
