using System;
using System.IO;
using System.Linq;
using UnityEngine;
using Zvezda57.CameraSystem;
using Zvezda57.Core;
using Zvezda57.Dispatch;
using Zvezda57.Documents;
using Zvezda57.InputSystem;
using Zvezda57.UI;
using Zvezda57.Visitors;

namespace Zvezda57.Systems
{
    public sealed class InvestigationController : MonoBehaviour, IDocumentDeskReview, IDeskToolActions
    {
        private IReviewService _review;
        private IDispatchService _dispatch;
        private IDecisionStore _journal;
        private DeskHud _hud;
        private ReferenceDefinition _reference;
        private CameraRig _rig;
        private MouseInteractor _mouse;
        [SerializeField]
        private DeskDevices deskDevices;
        [SerializeField]
        private DispatchPresenter dispatchPresenter;
        [SerializeField]
        private YuraPresenter yura;

        private InvestigationHud _overlay;
        private OperationsHud _operationsHud;
        private ICampaignService _campaign;
        private bool _active;
        private int _inspectedPage;
        internal int CrewCapacity => dispatchPresenter != null ? dispatchPresenter.CrewCapacity : 0;
        internal bool HasSelectedRepairCrew => dispatchPresenter.IsSelected(CrewType.Repair);
        internal Transform SectorTarget(int number) => dispatchPresenter.SectorTarget(number);

        public void Configure(IReviewService review, IDispatchService dispatch, IDecisionStore journal, ReferenceDefinition reference, DeskHud hud, CameraRig rig, MouseInteractor mouse, ICityMetrics metrics, IUpgradeService upgrades, ICampaignService campaign)
        {
            _reference = reference;
            _campaign = campaign;
            _review = review;
            _dispatch = dispatch;
            _journal = journal;
            _hud = hud;
            _rig = rig;
            _mouse = mouse;
            _overlay = GetComponent<InvestigationHud>();
            _overlay.Build(hud, reference, review.Call, ShowJournal);
            _operationsHud = GetComponent<OperationsHud>();
            _operationsHud.Configure(metrics, upgrades);
            yura.Build(campaign);
            deskDevices.Bind(this, campaign);
            dispatchPresenter.Build(dispatch, hud.SetHint, _overlay.ShowSector, rig);
            hud.ScanPoint += Scan;
            rig.Changed += CameraChanged;
            dispatch.Changed += RefreshAlerts;
            RefreshAlerts();
            ToolStatus();
        }

        void IDeskToolActions.SelectStamp(CaseVerdict verdict)
        {
            _review.SelectStamp(verdict);
            ToolStatus();
        }

        void IDeskToolActions.InkStamp()
        {
            bool inked = _review.Ink();
            _hud.SetHint(inked ? "Штамп смочен. Кликните по листу." : "Сначала возьмите штамп.");
            ToolStatus();
        }

        void IDeskToolActions.OpenReference()
        {
            _overlay.ShowReference(_reference);
        }

        void IDeskToolActions.SelectTool(InspectionTool tool)
        {
            bool selected = _review.SelectTool(tool);
            ToolStatus();
            if (!selected)
            {
                _hud.SetHint("Прибор откроется после вводного инструктажа смены 2.");
            }
        }

        void IDeskToolActions.AnswerCall()
        {
            if (!_campaign.AnswerCall())
            {
                _hud.SetHint("Входящих звонков нет.");
            }
        }

        public void CaptureDesk(DeskSnapshot state)
        {
            state.ReferenceOpen = _overlay.ReferenceOpen;
            state.ReferenceTab = _overlay.ReferenceTab;
            state.PhoneDigits = _overlay.PhoneDigits;
            state.PhoneAnswer = _overlay.PhoneAnswer;
        }

        public void RestoreDesk(DeskSnapshot state)
        {
            _overlay.RestoreReference(_reference, state.ReferenceOpen, state.ReferenceTab);
            if (state.Camera == CameraMode.Phone)
            {
                _overlay.RestorePhone(state.PhoneDigits, state.PhoneAnswer);
            }

            ToolStatus();
        }

        public void Show()
        {
            _active = true;
            _operationsHud.Show();
            _overlay.Show();
        }

        private void RefreshAlerts()
        {
            _overlay.Alerts(_dispatch);
        }

        private void ToolStatus()
        {
            if (_review.SelectedStamp is CaseVerdict verdict)
            {
                string nextAction = _review.Inked ? "поставьте оттиск" : "к подушке";
                _overlay.ToolStatus($"Штамп: {InvestigationHud.VerdictName(verdict)} · {nextAction}");
                return;
            }

            string message = _review.Tool switch
            {
                InspectionTool.Magnifier => "Выбран прибор: лупа",
                InspectionTool.Ultraviolet => "Выбран прибор: УФ",
                _ => string.Empty
            };
            _overlay.ToolStatus(message);
        }

        public void InspectPage(int page)
        {
            _inspectedPage = page;
            _overlay.ScanResult("");
        }

        private void Scan(Vector2 point)
        {
            if (_rig.Mode == CameraMode.Inspect && _review.Tool != InspectionTool.None)
            {
                _overlay.ShowProbe(point, _review.Tool);
                _overlay.ScanResult(_review.Scan(_inspectedPage, point));
            }
        }

        public bool TryStamp(int page)
        {
            if (!_review.SelectedStamp.HasValue)
            {
                return false;
            }

            _hud.SetHint(_review.Stamp(page) ? "Оттиск поставлен. До сдачи можно выбрать другой штамп." : "Оттиск не поставлен: сначала коснитесь подушки.");
            ToolStatus();
            return true;
        }

        public string StampText(int page)
        {
            return _review.GetStamp(page) is CaseVerdict verdict
                ? $"\n\n[ {InvestigationHud.VerdictName(verdict)} ]"
                : string.Empty;
        }

        public void Submit(CaseDefinition currentCase)
        {
            if (!_campaign.CanInteract)
            {
                return;
            }

            if (_review.PendingVerdict is not CaseVerdict verdict)
            {
                _hud.SetHint("Для сдачи нужен оттиск: штамп → подушка → лист.");
                return;
            }

            _operationsHud.Close();
            _mouse.CancelDrag();
            _mouse.Blocked = true;
            _overlay.Confirm(verdict, () => ConfirmSubmission(currentCase), () => _mouse.Blocked = false);
        }

        private void ConfirmSubmission(CaseDefinition currentCase)
        {
            _mouse.Blocked = false;
            try
            {
                var decision = _campaign.SubmitCase();
                if (decision == null)
                {
                    return;
                }

                if (_campaign.CanInteract)
                {
                    string verdict = InvestigationHud.VerdictName(decision.Verdict);
                    Sprite expression = decision.Verdict == CaseVerdict.Approve || currentCase.ReactionPortrait == null
                        ? currentCase.Portrait : currentCase.ReactionPortrait;
                    _overlay.ShowReceipt($"{verdict}\n\n{decision.Feedback}", expression);
                }
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
            {
                _hud.SetHint("Не удалось записать сохранение: " + exception.Message);
            }
        }

        private void ShowJournal()
        {
            if (!_active)
            {
                return;
            }

            _overlay.ShowJournal(_journal.Records.Count == 0 ? "Решений пока нет." : string.Join("\n", _journal.Records.Select(x => x.CaseId + " · " + InvestigationHud.VerdictName(x.Verdict) + " · " + x.Feedback)));
        }

        private void CameraChanged(CameraMode mode)
        {
            _operationsHud.Close();
            _overlay.CloseAll();
            if (_active && mode == CameraMode.Phone)
            {
                _overlay.Phone();
            }
        }

        public void ResetCase()
        {
            _review.Reset();
            ToolStatus();
        }

        public void Back()
        {
            _overlay.CloseAll();
        }

        private void OnDestroy()
        {
            if (_hud != null)
            {
                _hud.ScanPoint -= Scan;
            }

            if (_rig != null)
            {
                _rig.Changed -= CameraChanged;
            }

            if (_dispatch != null)
            {
                _dispatch.Changed -= RefreshAlerts;
            }
        }
    }
}
