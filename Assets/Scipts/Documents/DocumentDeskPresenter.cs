using System.Collections.Generic;
using UnityEngine;
using Zvezda57.CameraSystem;
using Zvezda57.Core;
using Zvezda57.UI;
using Zvezda57.Visitors;

namespace Zvezda57.Documents
{
    internal sealed class DocumentDeskPresenter
    {
        private readonly ICaseQueue _queue;
        private readonly IDocumentDeskReview _documentReview;
        private readonly CameraRig _cameraRig;
        private readonly DeskHud _hud;
        private readonly VisitorPresenter _visitor;
        private readonly GameObject _folder;
        private readonly DocumentFolderVisual _folderVisual;
        private readonly DraggableDocument[] _documentSlots;
        private readonly Transform _intakePoint;
        private readonly Transform _deskFolderPoint;
        private readonly Vector3 _openedFolderScaleMultiplier;
        private readonly Vector3 _closedFolderScale;
        private readonly List<DraggableDocument> _sheets = new List<DraggableDocument>();
        private bool _opened;
        private int _page;

        public DocumentDeskPresenter(
            ICaseQueue queue,
            IDocumentDeskReview documentReview,
            CameraRig cameraRig,
            DeskHud hud,
            VisitorPresenter visitor,
            GameObject folder,
            DraggableDocument[] documentSlots,
            Transform intakePoint,
            Transform deskFolderPoint,
            Vector3 openedFolderScaleMultiplier)
        {
            _queue = queue;
            _documentReview = documentReview;
            _cameraRig = cameraRig;
            _hud = hud;
            _visitor = visitor;
            _folder = folder;
            _folderVisual = folder.GetComponent<DocumentFolderVisual>();
            _documentSlots = documentSlots;
            foreach (var document in _documentSlots)
            {
                document.gameObject.SetActive(false);
            }
            _intakePoint = intakePoint;
            _deskFolderPoint = deskFolderPoint;
            _openedFolderScaleMultiplier = openedFolderScaleMultiplier;
            _closedFolderScale = folder.transform.localScale;
        }

        public bool FolderOpened => _opened;

        public DeskSnapshot CaptureDesk()
        {
            var state = new DeskSnapshot
            {
                CaseId = _queue.Current?.Id,
                Opened = _opened,
                Page = _page,
                Zoom = _hud.Zoom,
                Camera = _cameraRig.Mode,
                PaperPositions = _sheets.ConvertAll(x => x.SavedPosition).ToArray()
            };
            _documentReview.CaptureDesk(state);
            return state;
        }

        public void RestoreDesk(DeskSnapshot state)
        {
            if (state == null)
            {
                return;
            }

            if (state.CaseId == _queue.Current?.Id && _queue.State == IntakeState.Received)
            {
                SetFolderOpen(state.Opened);
                _page = Mathf.Clamp(state.Page, 0, _queue.Current.Documents.Length - 1);
                _hud.Zoom = state.Zoom;
                for (int i = 0; i < _sheets.Count; i++)
                {
                    if (state.PaperPositions != null && i < state.PaperPositions.Length)
                    {
                        _sheets[i].SetSavedPosition(state.PaperPositions[i]);
                    }

                    _sheets[i].SetTitle(_queue.Current.Documents[i].Title + _documentReview.StampText(i));
                }

                _cameraRig.Go(state.Camera);
                if (state.Camera == CameraMode.Inspect)
                {
                    ShowPage();
                }
            }
            else
            {
                _cameraRig.Go(state.Camera == CameraMode.Inspect ? CameraMode.Desk : state.Camera);
            }

            _documentReview.RestoreDesk(state);
        }

        public void InteractWithFolder()
        {
            if (_queue.State == IntakeState.Offered)
            {
                var intakeCamera = _queue.Current.DeliveredByYura ? CameraMode.Desk : CameraMode.Window;
                if (_cameraRig.Mode != intakeCamera)
                {
                    _cameraRig.Go(intakeCamera);
                    return;
                }

                _queue.Receive();
                _cameraRig.Go(CameraMode.Desk);
                return;
            }

            if (_queue.State != IntakeState.Received)
            {
                return;
            }

            if (_cameraRig.Mode != CameraMode.Desk)
            {
                _cameraRig.Go(CameraMode.Desk);
                return;
            }

            SetFolderOpen(!_opened);
            _hud.SetHint(_opened ? "Кликните по листу для чтения. Удерживайте ЛКМ для переноса." : "Папка закрыта.");
        }

        public void Refresh()
        {
            if (_queue.State == IntakeState.Offered)
            {
                _documentReview.ResetCase();
            }

            foreach (var sheet in _sheets)
            {
                sheet.gameObject.SetActive(false);
            }

            _sheets.Clear();
            SetFolderOpen(false);
            switch (_queue.State)
            {
                case IntakeState.Offered:
                    ShowOfferedFolder(_queue.Current);
                    break;
                case IntakeState.Received:
                    ShowReceivedFolder(_queue.Current);
                    break;
                default:
                    HideFolder();
                    break;
            }
        }

        private void ShowOfferedFolder(CaseDefinition currentCase)
        {
            if (currentCase.DeliveredByYura)
            {
                _visitor.Hide();
            }
            else
            {
                _visitor.Show(currentCase);
                if (_cameraRig.Mode == CameraMode.Window)
                {
                    _visitor.Speak();
                }
            }

            _folder.SetActive(true);
            _folder.transform.position = currentCase.DeliveredByYura
                ? _deskFolderPoint.position
                : _intakePoint.position;
            _hud.SetHint(currentCase.Greeting);
        }

        public void GreetVisitor()
        {
            if (_queue.State == IntakeState.Offered && !_queue.Current.DeliveredByYura)
            {
                _visitor.Speak();
                _hud.SetHint(_queue.Current.Greeting);
            }
        }

        private void ShowReceivedFolder(CaseDefinition currentCase)
        {
            _visitor.Hide();
            _folder.transform.position = _deskFolderPoint.position;
            for (int page = 0; page < currentCase.Documents.Length; page++)
            {
                AddDocument(page, currentCase.Documents[page].Title);
            }
        }

        private void AddDocument(int page, string title)
        {
            var document = _documentSlots[page];
            document.SetTitle(title);
            document.Configure(() =>
            {
                if (_documentReview.TryStamp(page))
                {
                    document.SetTitle(title + _documentReview.StampText(page));
                }
                else
                {
                    Inspect(page);
                }
            });
            _sheets.Add(document);
        }

        private void HideFolder()
        {
            _folder.SetActive(false);
            _visitor.Hide();
            _hud.SetHint(_queue.State == IntakeState.Complete
                ? "Все папки прототипа просмотрены."
                : "Папка в архиве. Можно вызвать следующего посетителя.");
        }

        private void Inspect(int index)
        {
            _page = index;
            _cameraRig.Go(CameraMode.Inspect);
            ShowPage();
        }

        public void TurnPage(int delta)
        {
            if (_cameraRig.Mode != CameraMode.Inspect || _queue.State != IntakeState.Received)
            {
                return;
            }

            int pageCount = _queue.Current.Documents.Length;
            _page = ((_page + delta) % pageCount + pageCount) % pageCount;
            ShowPage();
        }

        private void ShowPage()
        {
            _documentReview.InspectPage(_page);
            var currentCase = _queue.Current;
            var documents = currentCase.Documents;
            var document = documents[_page];
            Sprite photo = currentCase.DocumentPhoto != null
                ? (_page == 0 ? currentCase.DocumentPhoto : null)
                : currentCase.Portrait;
            _hud.ShowDocument(document, photo, _documentReview.StampText(_page), _page, documents.Length);
        }

        private void SetFolderOpen(bool opened)
        {
            _opened = opened;
            _folder.transform.localScale = opened
                ? Vector3.Scale(_closedFolderScale, _openedFolderScaleMultiplier)
                : _closedFolderScale;
            _folderVisual?.Show(opened);

            foreach (var sheet in _sheets)
            {
                sheet.gameObject.SetActive(opened);
            }
        }
    }
}
