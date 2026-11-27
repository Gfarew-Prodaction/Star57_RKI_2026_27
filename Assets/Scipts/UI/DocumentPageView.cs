using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Zvezda57.Documents;

namespace Zvezda57.UI
{
    public sealed class DocumentPageView : MonoBehaviour
    {
        [SerializeField]
        private DocumentForm form;
        [SerializeField]
        private TMP_Text heading;
        [SerializeField]
        private TMP_Text body;
        [SerializeField]
        private TMP_Text stamp;
        [SerializeField]
        private Image portrait;
        [SerializeField]
        private bool reservePhotoColumn;

        private Vector2 _bodyAnchorMax;

        public DocumentForm Form => form;
        public RectTransform ScanArea => body.rectTransform;

        private void Awake()
        {
            _bodyAnchorMax = body.rectTransform.anchorMax;
        }

        public void Show(DocumentContent document, Sprite visitorPortrait, string impression, float zoom)
        {
            heading.text = document.Title;
            body.text = (document.Body ?? string.Empty).Replace("\\n", "\n");
            stamp.text = impression.Trim(' ', '\n', '\r', '[', ']');
            stamp.gameObject.SetActive(stamp.text.Length > 0);
            if (portrait != null)
            {
                portrait.sprite = visitorPortrait;
                portrait.gameObject.SetActive(visitorPortrait != null);
                if (reservePhotoColumn)
                {
                    var anchorMax = _bodyAnchorMax;
                    if (visitorPortrait != null)
                    {
                        anchorMax.x = portrait.rectTransform.anchorMin.x - .04f;
                    }

                    body.rectTransform.anchorMax = anchorMax;
                }
            }

            SetZoom(zoom);
        }

        public void SetZoom(float zoom)
        {
            body.fontSizeMax = zoom;
            body.fontSize = zoom;
        }
    }
}
