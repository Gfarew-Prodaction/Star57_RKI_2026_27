using UnityEngine.InputSystem.UI;
using UnityEngine;
using VContainer;
using Zvezda57.CameraSystem;
using Zvezda57.Dispatch;
using Zvezda57.Documents;
using Zvezda57.InputSystem;
using Zvezda57.Systems;
using Zvezda57.UI;
using Zvezda57.Visitors;

namespace Zvezda57.Core
{
    public sealed class OfficePrototype : MonoBehaviour
    {
        [SerializeField]
        private CameraRig rig;
        [SerializeField]
        private GameObject folder;
        [SerializeField]
        private VisitorPresenter visitor;
        [SerializeField]
        private DraggableDocument[] documentSlots;
        [SerializeField]
        private InputSystemUIInputModule input;
        [SerializeField]
        private Transform intakePoint, deskFolderPoint;
        [SerializeField]
        private Vector3 openedFolderScaleMultiplier = new Vector3(1.285714f, .714286f, 1.3f);

        private ICaseQueue _queue;
        private DeskHud _hud;
        private MouseInteractor _mouse;
        private DocumentDeskPresenter _documentDesk;

        internal int DocumentCapacity => documentSlots?.Length ?? 0;

        private bool _started;
        private InvestigationController _investigation;
        private IReviewService _review;
        private IDecisionStore _journal;
        private IDispatchService _dispatch;
        private ReferenceDefinition _reference;
        private ICityMetrics _metrics;
        private IUpgradeService _upgrades;
        private ICampaignService _campaign;
        private CampaignHud _campaignHud;
        [Inject]
        public void Construct(ICaseQueue cases, IReviewService review, IDecisionStore journal, IDispatchService dispatch, ReferenceDefinition reference, ICityMetrics metrics, IUpgradeService upgrades, ICampaignService campaign)
        {
            _queue = cases;
            _review = review;
            _journal = journal;
            _dispatch = dispatch;
            _reference = reference;
            _metrics = metrics;
            _upgrades = upgrades;
            _campaign = campaign;
        }

        public void Initialize()
        {
            _hud = GetComponent<DeskHud>();
            _mouse = GetComponent<MouseInteractor>();
            _mouse.Configure(rig.GetComponent<Camera>(), rig);
            _mouse.Blocked = true;
            input.AssignDefaultActions();

            _hud.Build(Back, Next, File);
            _investigation = GetComponent<InvestigationController>();
            _investigation.Configure(_review, _dispatch, _journal, _reference, _hud, rig, _mouse, _metrics, _upgrades, _campaign);
            GetComponent<OfficeEquipmentPresenter>()?.Bind(_metrics, _dispatch, _campaign, Next,
                GetComponent<OperationsHud>().OpenUpgrades, _hud.SetHint);
            _documentDesk = new DocumentDeskPresenter(
                _queue, _investigation, rig, _hud, visitor, folder,
                documentSlots, intakePoint, deskFolderPoint, openedFolderScaleMultiplier);

            System.Action restart = () => SceneNavigation.OpenGame(true);
            _campaignHud = GetComponent<CampaignHud>();
            _campaignHud.Build(_hud, _campaign, _metrics, _mouse, rig, restart, _dispatch);
            visitor.SetFont(_hud.Font);
            GetComponent<CompactHud>()?.Bind(_campaign, _queue, _review, rig);
            var guide = GetComponent<TutorialGuide>();
            if (guide != null)
            {
                guide.Bind(_campaign, _queue, _review, _dispatch, rig,
                    () => _documentDesk.FolderOpened, () => _investigation.HasSelectedRepairCrew, _investigation.SectorTarget);
            }

            _hud.CloseReader += Back;
            _hud.TurnPage += _documentDesk.TurnPage;
            _mouse.HintChanged += _hud.SetHint;
            rig.Changed += OnCameraChanged;
            _queue.Changed += Refresh;
            folder.GetComponent<WorldAction>().Configure("Окошко: получить папку / стол: открыть или закрыть", FolderClick);
            foreach (var zone in zones)
            {
                zone.Bind(rig);
            }

            folder.SetActive(false);
            visitor.Hide();
            rig.Go(CameraMode.Overview);
            _hud.SetStatus("13 октября 1957 · Начните смену");
        }

        private void Start()
        {
            if (_campaignHud == null)
            {
                return;
            }

            _started = true;
            _campaignHud.Show();
            _investigation.Show();
            _campaign.Start();
            _documentDesk.RestoreDesk(_campaign.Desk);
            _campaign.BindView(_documentDesk.CaptureDesk);
            SceneNavigation.LeavingGame += SaveSession;
            _mouse.PauseRequested += Pause;
        }

        private void Update()
        {
            if (_started && !SceneNavigation.IsLoading)
            {
                _campaign.Tick(Time.deltaTime);
            }
        }

        private void Refresh()
        {
            _documentDesk.Refresh();
            UpdateStatus();
        }

        private void FolderClick()
        {
            if (_campaign.CanInteract)
            {
                _documentDesk.InteractWithFolder();
            }
        }

        private void Pause()
        {
            _mouse.CancelDrag();
            _campaign.TogglePause();
        }

        private void SaveSession()
        {
            _mouse.CancelDrag();
            _campaign.Checkpoint();
        }

        private void OnApplicationQuit()
        {
            if (_started && !SceneNavigation.IsLoading)
            {
                SaveSession();
            }
        }

        private void OnApplicationFocus(bool focused)
        {
            if (_started && !focused && !SceneNavigation.IsLoading)
            {
                SaveSession();
            }
        }

        [SerializeField]
        private CameraZone[] zones;

        private void OnCameraChanged(CameraMode mode)
        {
            _mouse.CancelDrag();
            if (mode != CameraMode.Inspect)
            {
                _hud.HideDocument();
            }

            if (mode == CameraMode.Window)
            {
                _documentDesk.GreetVisitor();
            }

            _mouse.Blocked = !_started || !_campaign.CanInteract || mode == CameraMode.Inspect;
            UpdateStatus();
        }

        private void Next()
        {
            if (!_started || !_campaign.CanInteract || rig.Mode == CameraMode.Inspect)
            {
                return;
            }

            if (!_queue.OfferNext())
            {
                _hud.SetHint("Сначала получите и уберите текущую папку в архив.");
            }
        }

        private void File()
        {
            if (!_started || !_campaign.CanInteract || rig.Mode != CameraMode.Desk)
            {
                _hud.SetHint("Архив доступен за столом.");
                return;
            }

            _investigation.Submit(_queue.Current);
        }

        private void Back()
        {
            if (_started && _campaign.CanInteract)
            {
                _investigation.Back();
                rig.Back();
            }
        }

        private void UpdateStatus()
        {
            var currentCase = _queue.Current;
            string caseCaption = currentCase != null && _queue.State != IntakeState.Filed ? " · Дело " + currentCase.Id : "";
            _hud.SetStatus(ModeCaption(rig.Mode) + caseCaption);
        }

        private static string ModeCaption(CameraMode mode) => new[]
        {
            "Кабинет",
            "Приём",
            "Стол",
            "Карта",
            "Телефон",
            "Чтение"
        }[(int)mode];
        private void OnDestroy()
        {
            SceneNavigation.LeavingGame -= SaveSession;
            if (_mouse != null)
            {
                _mouse.PauseRequested -= Pause;
                _mouse.HintChanged -= _hud.SetHint;
            }

            if (_queue != null)
            {
                _queue.Changed -= Refresh;
            }

            if (rig != null)
            {
                rig.Changed -= OnCameraChanged;
            }

            if (_hud != null)
            {
                _hud.CloseReader -= Back;
                if (_documentDesk != null)
                {
                    _hud.TurnPage -= _documentDesk.TurnPage;
                }
            }
        }
    }
}
