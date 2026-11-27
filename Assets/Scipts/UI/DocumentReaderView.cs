using TMPro;
using UnityEngine;
using Zvezda57.Documents;

namespace Zvezda57.UI
{
    public sealed class DocumentReaderView : MonoBehaviour
    {
        [SerializeField]
        private DocumentPageView[] forms;
        [SerializeField]
        private TMP_Text pageNumber;
        private DocumentPageView _current;

        public RectTransform ScanArea => _current != null ? _current.ScanArea : null;

        public void Show(DocumentContent document, Sprite portrait, string impression, int page, int total, float zoom)
        {
            var selected = System.Array.Find(forms, view => view.Form == document.Form) ?? forms[0];
            foreach (var view in forms)
            {
                view.gameObject.SetActive(view == selected);
            }

            _current = selected;
            _current.Show(document, portrait, impression, zoom);
            pageNumber.text = $"Лист {page + 1} / {total}";
        }

        public void SetZoom(float zoom)
        {
            _current?.SetZoom(zoom);
        }
    }
}
