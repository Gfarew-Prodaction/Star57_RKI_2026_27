using TMPro;
using UnityEngine;
using Zvezda57.CameraSystem;
using Zvezda57.InputSystem;

namespace Zvezda57.Documents
{
    public sealed class DraggableDocument : MonoBehaviour, IWorldAction, IWorldDrag
    {
        [SerializeField]
        private Renderer highlightRenderer;
        [SerializeField]
        private TMP_Text title;
        private Vector3 _safePosition;
        private Vector3 _offset;
        [SerializeField]
        private BoxCollider dragArea;
        private Plane _plane;
        private bool _configured;
        private Vector3 _home;
        public void ConfigureArea(BoxCollider area)
        {
            dragArea = area;
        }

        private System.Action _inspect;
        private Renderer _surface;
        private Color _original;
        public Vector3 SavedPosition => _safePosition;

        public void SetSavedPosition(Vector3 value)
        {
            transform.position = value;
            _safePosition = value;
        }

        internal void SetTitle(string value)
        {
            title.text = value;
        }

        public string Hint => "ЛКМ — прочесть · удерживать — перенести лист";

        public void Configure(System.Action inspectAction)
        {
            if (!_configured)
            {
                _home = transform.position;
                _configured = true;
            }
            else
            {
                transform.position = _home;
            }

            _inspect = inspectAction;
            _safePosition = transform.position;
            _plane = new Plane(dragArea != null ? dragArea.transform.up : Vector3.up, _safePosition);
            _surface = highlightRenderer != null ? highlightRenderer : GetComponent<Renderer>();
            _original = _surface.sharedMaterial.color;
        }

        public bool CanUse(CameraMode mode) => mode == CameraMode.Desk;
        public void Use()
        {
            _inspect?.Invoke();
        }

        public void Hover(bool active)
        {
            _surface.material.color = active ? Color.white : _original;
        }

        public void Begin(Ray ray)
        {
            _safePosition = transform.position;
            if (_plane.Raycast(ray, out var distance))
            {
                _offset = transform.position - ray.GetPoint(distance);
            }
        }

        public void Drag(Ray ray)
        {
            if (_plane.Raycast(ray, out var distance))
            {
                transform.position = ray.GetPoint(distance) + _offset;
            }
        }

        public void End(bool cancel = false)
        {
            var p = transform.position;
            var local = dragArea != null ? dragArea.transform.InverseTransformPoint(p) - dragArea.center : p;
            bool outside = dragArea != null ? Mathf.Abs(local.x) > dragArea.size.x * .5f || Mathf.Abs(local.z) > dragArea.size.z * .5f : Mathf.Abs(p.x) > 1.65f || p.z < -1f || p.z > .9f;
            if (cancel || outside)
            {
                transform.position = _safePosition;
            }
            else
            {
                _safePosition = p;
            }
        }

        public void End(Ray ray, bool cancel)
        {
            End(cancel);
        }

        private void OnDisable()
        {
            if (_configured)
            {
                End(true);
            }
        }
    }
}
