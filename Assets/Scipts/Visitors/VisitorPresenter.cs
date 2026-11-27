using DG.Tweening;
using TMPro;
using UnityEngine;
using Zvezda57.Documents;

namespace Zvezda57.Visitors
{
    public sealed class VisitorPresenter : MonoBehaviour
    {
        [SerializeField]
        private SpriteRenderer portrait;
        [SerializeField]
        private TextMeshPro label;
        private Tween _arrival;
        private Tween _speech;
        private Sprite _fallback;
        private CaseDefinition _visitor;
        private Vector3 _restPosition;

        private void Awake()
        {
            _fallback = portrait.sprite;
            _restPosition = portrait.transform.localPosition;
        }

        public void SetFont(TMP_FontAsset font)
        {
            label.font = font;
        }

        public void Show(CaseDefinition data)
        {
            _arrival?.Kill();
            _speech?.Kill();
            _visitor = data;
            portrait.sprite = data.Portrait != null ? data.Portrait : _fallback;
            portrait.color = data.PortraitTint;
            portrait.gameObject.SetActive(true);
            label.text = data.VisitorName;
            portrait.transform.localPosition = _restPosition;
            if (!Application.isPlaying)
            {
                return;
            }

            portrait.transform.localPosition -= Vector3.right * .5f;
            _arrival = portrait.transform.DOLocalMoveX(_restPosition.x, .7f).SetEase(Ease.OutSine);
        }

        public void Speak()
        {
            if (_visitor == null || _visitor.SpeakingPortrait == null)
            {
                return;
            }

            _speech?.Kill();
            portrait.sprite = _visitor.SpeakingPortrait;
            var neutral = _visitor.Portrait != null ? _visitor.Portrait : _fallback;
            _speech = DOVirtual.DelayedCall(1.65f, () => portrait.sprite = neutral, false);
        }

        public void Hide()
        {
            _arrival?.Kill();
            _speech?.Kill();
            _visitor = null;
            portrait.gameObject.SetActive(false);
            label.text = "";
        }

        private void OnDestroy()
        {
            _arrival?.Kill();
            _speech?.Kill();
        }
    }
}
