using UnityEngine;
using Zvezda57.Systems;

namespace Zvezda57.Visitors
{
    public sealed class IncomingPhoneIndicator : MonoBehaviour
    {
        [SerializeField]
        private Renderer dialIndicator;
        private ICampaignPlayState _campaign;
        private Renderer _surface;
        private Color _idleColor;
        private MaterialPropertyBlock _properties;
        private AudioSource _audioSource;
        private AudioClip _signal;
        private float _nextSignal;
        private bool _ownsSignal;
        public void Configure(ICampaignPlayState campaign)
        {
            _campaign = campaign;
            _surface = dialIndicator != null ? dialIndicator : GetComponent<Renderer>();
            _idleColor = _surface.sharedMaterial.color;
            _properties = new MaterialPropertyBlock();
            _audioSource = GetComponent<AudioSource>();

            if (_audioSource.clip != null)
            {
                _signal = _audioSource.clip;
                return;
            }

            _ownsSignal = true;
            const int rate = 22050;
            var samples = new float[rate / 5];
            for (int i = 0; i < samples.Length; i++)
            {
                samples[i] = Mathf.Sin(2 * Mathf.PI * 660 * i / rate) * Mathf.Sin(Mathf.PI * i / samples.Length);
            }

            _signal = AudioClip.Create("Government phone signal", samples.Length, 1, rate, false);
            _signal.SetData(samples, 0);
            _audioSource.clip = _signal;
        }

        private void Update()
        {
            if (_campaign == null)
            {
                return;
            }

            bool ringing = _campaign.CallState == IncomingCallState.Ringing && _campaign.CanInteract;
            _properties.SetColor("_BaseColor", ringing ? Color.Lerp(_idleColor, new Color(1, .4f, .15f), .5f + .5f * Mathf.Sin(Time.unscaledTime * 8)) : _idleColor);
            _surface.SetPropertyBlock(_properties);
            if (ringing && Time.unscaledTime >= _nextSignal)
            {
                _audioSource.Play();
                _nextSignal = Time.unscaledTime + 2;
            }

            if (!ringing && _audioSource.isPlaying)
            {
                _audioSource.Stop();
            }
        }

        private void OnDestroy()
        {
            if (_ownsSignal && _signal != null)
            {
                Destroy(_signal);
            }
        }
    }
}
