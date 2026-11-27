using System;
using TMPro;
using UnityEngine.UI;
using UnityEngine;
using Zvezda57.Core;
using Zvezda57.Documents;

namespace Zvezda57.UI
{
    public sealed class DeskHud : MonoBehaviour
    {
        [SerializeField]
        private TMP_FontAsset font;
        [SerializeField]
        private Canvas readerCanvas;
        [SerializeField]
        private GameObject reader;
        [SerializeField]
        private DocumentReaderView documentPages;
        [SerializeField]
        private TMP_Text status, hint;
        [SerializeField]
        private Button backButton, nextVisitorButton, fileButton, menuButton, previousPageButton, nextPageButton, closeReaderButton;

        public TMP_FontAsset Font => font;
        public RectTransform DocumentRect => documentPages.ScanArea;
        public event Action<Vector2> ScanPoint;
        private float _hintUntil;
        private float _zoom = 26;
        public float Zoom
        {
            get => _zoom;
            set
            {
                _zoom = Mathf.Clamp(value, 22, 36);
                documentPages?.SetZoom(_zoom);
            }
        }

        public event Action CloseReader;
        public event Action<int> TurnPage;
        public void Build(Action back, Action next, Action file)
        {
            backButton.onClick.AddListener(() => back());
            nextVisitorButton.onClick.AddListener(() => next());
            fileButton.onClick.AddListener(() => file());
            menuButton.onClick.AddListener(SceneNavigation.OpenMenu);
            previousPageButton.onClick.AddListener(() => TurnPage?.Invoke(-1));
            nextPageButton.onClick.AddListener(() => TurnPage?.Invoke(1));
            closeReaderButton.onClick.AddListener(() => CloseReader?.Invoke());
            reader.SetActive(false);
        }

        public void SetStatus(string value)
        {
            status.text = value;
        }

        public void SetHint(string value)
        {
            hint.text = value;
            _hintUntil = Time.unscaledTime + 5;
        }

        public void ShowDocument(DocumentContent document, Sprite portrait, string impression, int page, int total)
        {
            reader.SetActive(true);
            documentPages.Show(document, portrait, impression, page, total, _zoom);
        }

        public void HideDocument()
        {
            reader.SetActive(false);
        }

        private void Update()
        {
            if (hint != null && Time.unscaledTime > _hintUntil)
            {
                hint.text = "";
            }

            if (!reader || !reader.activeSelf || DocumentRect == null || UnityEngine.InputSystem.Mouse.current == null)
            {
                return;
            }

            var mouse = UnityEngine.InputSystem.Mouse.current;
            var scanArea = DocumentRect;
            var eventCamera = readerCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : readerCanvas.worldCamera;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(scanArea, mouse.position.ReadValue(), eventCamera, out var local))
            {
                var rect = scanArea.rect;
                var normalized = new Vector2((local.x - rect.xMin) / rect.width, (local.y - rect.yMin) / rect.height);
                if (normalized.x >= 0 && normalized.x <= 1 && normalized.y >= 0 && normalized.y <= 1)
                {
                    ScanPoint?.Invoke(normalized);
                }
            }

            float scroll = UnityEngine.InputSystem.Mouse.current.scroll.ReadValue().y;
            if (Mathf.Abs(scroll) > .01f)
            {
                Zoom += Mathf.Sign(scroll) * 2;
            }
        }
    }
}
