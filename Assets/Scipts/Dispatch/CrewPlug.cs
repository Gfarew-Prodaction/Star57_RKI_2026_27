using System;
using TMPro;
using UnityEngine;
using Zvezda57.CameraSystem;
using Zvezda57.InputSystem;

namespace Zvezda57.Dispatch
{
    public sealed class CrewPlug : MonoBehaviour, IWorldAction, IWorldDrag
    {
        [SerializeField]
        private Renderer highlightRenderer;
        [SerializeField]
        private TMP_Text label;

        public TMP_Text Label => label;
        private int _crewId;
        private IDispatchStatus _dispatch;
        private Action<int> _select;
        private Action<int, int> _drop;
        private Vector3 _home;
        private Renderer _surface;
        private Color _color;
        private Plane _dragPlane;
        public string Hint => "Выбрать штекер / перетащить в гнездо сектора";

        public void Configure(int crewId, IDispatchStatus dispatch, Action<int> select, Action<int, int> drop)
        {
            _crewId = crewId;
            _dispatch = dispatch;
            _select = select;
            _drop = drop;
            _home = transform.position;
            _surface = highlightRenderer != null ? highlightRenderer : GetComponent<Renderer>();
            _color = _surface.sharedMaterial.color;
            _dragPlane = new Plane(Vector3.forward, _home);
        }

        public bool CanUse(CameraMode mode) => mode == CameraMode.Map;
        public void Use()
        {
            _select(_crewId);
        }

        public void Hover(bool active)
        {
            _surface.material.color = active ? Color.white : _color;
        }

        public void Begin(Ray ray)
        {
        }

        public void Drag(Ray ray)
        {
            if (_dispatch.Crews[_crewId].State != CrewState.Free)
            {
                return;
            }

            if (_dragPlane.Raycast(ray, out var distance))
            {
                transform.position = ray.GetPoint(distance);
            }
        }

        public void End(Ray ray, bool cancel)
        {
            var collider = GetComponent<Collider>();
            collider.enabled = false;
            if (!cancel && Physics.Raycast(ray, out var hit, 100f))
            {
                var socket = hit.collider.GetComponent<SectorSocket>();
                if (socket != null)
                {
                    _drop(_crewId, socket.Sector);
                }
            }

            collider.enabled = true;
            transform.position = _home;
        }
    }
}
