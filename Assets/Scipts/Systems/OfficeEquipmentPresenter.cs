using System;
using System.Linq;
using UnityEngine;
using Zvezda57.CameraSystem;
using Zvezda57.Dispatch;
using Zvezda57.InputSystem;

namespace Zvezda57.Systems
{
    public sealed class OfficeEquipmentPresenter : MonoBehaviour
    {
        private static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");
        private static readonly float[] GaugeReference = { 76, 63, 82 };
        private const float GaugeSweep = 124;
        private const float FailureSignalSeconds = 3;

        [SerializeField]
        private Transform gaugePanel;
        [SerializeField]
        private Transform[] needles;
        [SerializeField]
        private Transform flagHinge;
        [SerializeField]
        private Renderer alarmGlobe;
        [SerializeField]
        private Light[] mainsLights;
        [SerializeField]
        private Renderer ceilingFixture;
        [SerializeField]
        private int ceilingBulbMaterialIndex;
        [SerializeField]
        private Light emergencyLight;
        [SerializeField]
        private WorldAction receptionBell;
        [SerializeField]
        private WorldAction callButton;
        [SerializeField]
        private WorldAction reservesClipboard;

        private MaterialPropertyBlock _alarmProperties;
        private MaterialPropertyBlock _unpoweredBulb;
        private ICityMetrics _metrics;
        private IDispatchService _dispatch;
        private Quaternion[] _needleRest;
        private Quaternion _flagRaised;
        private float _failureSignalUntil;
        private bool _blackout;

        public void Bind(ICityMetrics metrics, IDispatchService dispatch, ICampaignService campaign,
            Action callVisitor, Action showReserves, Action<string> feedback)
        {
            _metrics = metrics;
            _dispatch = dispatch;
            _alarmProperties = new MaterialPropertyBlock();
            _unpoweredBulb = new MaterialPropertyBlock();
            _unpoweredBulb.SetColor(EmissionColor, Color.black);
            _needleRest = needles.Select(needle => needle.localRotation).ToArray();
            _flagRaised = flagHinge.localRotation;
            receptionBell.Configure("Колокольчик · вызвать посетителя", callVisitor);
            callButton.Configure("Кнопка связи · поручить Юре проверку архива", () =>
            {
                campaign.Assign(YuraTask.Archive, out string message);
                feedback(message);
            }, CameraMode.Desk);
            reservesClipboard.Configure("Ведомость резервов", showReserves, CameraMode.Desk);

            _metrics.Changed += RefreshGauges;
            _dispatch.Changed += RefreshSignals;
            _dispatch.Finished += ShowFailureSignal;
            RefreshGauges();
            RefreshSignals();
        }

        private void RefreshGauges()
        {
            SetNeedle(0, _metrics.Reactor);
            SetNeedle(1, _metrics.Secrecy);
            SetNeedle(2, _metrics.Calm);
        }

        private void SetNeedle(int index, int reading)
        {
            var needle = needles[index];
            var axis = needle.parent.InverseTransformDirection(gaugePanel.forward);
            float angle = (reading - GaugeReference[index]) * GaugeSweep / 100f;
            needle.localRotation = Quaternion.AngleAxis(angle, axis) * _needleRest[index];
        }

        private void RefreshSignals()
        {
            var incidents = _dispatch.Incidents;
            bool blackout = incidents.Any(failure =>
                failure.State == IncidentState.Failed &&
                !string.IsNullOrEmpty(failure.PowerRestorationIncidentId) &&
                !incidents.Any(repair => repair.Id == failure.PowerRestorationIncidentId && repair.State == IncidentState.Resolved));
            bool failureSignal = Time.unscaledTime < _failureSignalUntil;
            bool incoming = incidents.Any(incident => incident.State == IncidentState.Waiting);
            flagHinge.localRotation = incoming || failureSignal
                ? _flagRaised * Quaternion.AngleAxis(45, Vector3.right)
                : _flagRaised;
            Color signal = blackout || failureSignal
                ? new Color(3f, .09f, .035f)
                : (incoming ? new Color(1.5f, .18f, .06f) : Color.black);
            _alarmProperties.SetColor(EmissionColor, signal);
            alarmGlobe.SetPropertyBlock(_alarmProperties);
            if (_blackout != blackout)
            {
                _blackout = blackout;
                foreach (var light in mainsLights)
                {
                    light.enabled = !blackout;
                }

                ceilingFixture.SetPropertyBlock(blackout ? _unpoweredBulb : null, ceilingBulbMaterialIndex);
            }

            emergencyLight.enabled = blackout || failureSignal;
        }

        private void ShowFailureSignal(IIncidentStatus incident)
        {
            if (incident.State == IncidentState.Failed)
            {
                _failureSignalUntil = Time.unscaledTime + FailureSignalSeconds;
            }

            RefreshSignals();
        }

        private void Update()
        {
            if (_failureSignalUntil > 0 && Time.unscaledTime >= _failureSignalUntil)
            {
                _failureSignalUntil = 0;
                RefreshSignals();
            }
        }

        private void OnDestroy()
        {
            if (_metrics != null)
            {
                _metrics.Changed -= RefreshGauges;
            }

            if (_dispatch != null)
            {
                _dispatch.Changed -= RefreshSignals;
                _dispatch.Finished -= ShowFailureSignal;
            }
        }
    }
}
