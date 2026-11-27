using System;
using DG.Tweening;
using UnityEngine;

namespace Zvezda57.CameraSystem
{
    public sealed class CameraRig : MonoBehaviour
    {
        [SerializeField]
        private CameraFocus[] focusPoints;
        private Sequence _transition;
        public CameraMode Mode { get; private set; }
        public bool IsMoving => _transition != null && _transition.IsActive() && _transition.IsPlaying();

        public event Action<CameraMode> Changed;
        public void Configure(CameraFocus[] points)
        {
            focusPoints = points;
        }

        public void Go(CameraMode mode)
        {
            foreach (var point in focusPoints)
            {
                if (point.Mode != mode)
                {
                    continue;
                }

                if (!Application.isPlaying)
                {
                    Mode = mode;
                    transform.SetPositionAndRotation(point.Position, Quaternion.Euler(point.Euler));
                    Changed?.Invoke(mode);
                    return;
                }

                _transition?.Kill();
                Mode = mode;
                _transition = DOTween.Sequence().Join(transform.DOMove(point.Position, .55f)).Join(transform.DORotate(point.Euler, .55f)).SetEase(Ease.InOutSine);
                Changed?.Invoke(mode);
                return;
            }
        }

        public void Back()
        {
            Go(Mode == CameraMode.Inspect ? CameraMode.Desk : CameraMode.Overview);
        }

        private void OnDestroy()
        {
            _transition?.Kill();
        }
    }
}
