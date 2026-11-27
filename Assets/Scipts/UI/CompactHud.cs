using UnityEngine.UI;
using UnityEngine;
using Zvezda57.CameraSystem;
using Zvezda57.Core;
using Zvezda57.Documents;
using Zvezda57.Systems;

namespace Zvezda57.UI
{
    public sealed class CompactHud : MonoBehaviour
    {
        [SerializeField]
        private GameObject header, footer, drawer;
        [SerializeField]
        private Button actions, back, next, submit, endShift;
        [SerializeField]
        private RectTransform viewNavigation;
        [SerializeField]
        private GameObject toolStatus;
        [SerializeField]
        private Button overviewView, deskView, receptionView, mapView, phoneView;
        private ICampaignPlayState _campaign;
        private ICaseQueue _queue;
        private IReviewProgress _review;
        private CameraRig _rig;
        public void Configure(GameObject header, GameObject footer, GameObject drawer, Button actions, Button back, Button next, Button submit, Button endShift)
        {
            this.header = header;
            this.footer = footer;
            this.drawer = drawer;
            this.actions = actions;
            this.back = back;
            this.next = next;
            this.submit = submit;
            this.endShift = endShift;
        }

        public void Bind(ICampaignPlayState campaign, ICaseQueue queue, IReviewProgress review, CameraRig rig)
        {
            _campaign = campaign;
            _queue = queue;
            _review = review;
            _rig = rig;
            actions.onClick.RemoveAllListeners();
            actions.onClick.AddListener(() => drawer.SetActive(!drawer.activeSelf));
            foreach (var button in drawer.GetComponentsInChildren<Button>(true))
            {
                button.onClick.AddListener(() => drawer.SetActive(false));
            }

            BindView(overviewView, CameraMode.Overview);
            BindView(deskView, CameraMode.Desk);
            BindView(receptionView, CameraMode.Window);
            BindView(mapView, CameraMode.Map);
            BindView(phoneView, CameraMode.Phone);
            _rig.Changed += ViewChanged;
            ViewChanged(_rig.Mode);
            drawer.SetActive(false);
        }

        private void BindView(Button button, CameraMode mode)
        {
            if (button != null)
            {
                button.onClick.AddListener(() =>
                {
                    drawer.SetActive(false);
                    _rig.Go(mode);
                });
            }
        }

        private void ViewChanged(CameraMode mode)
        {
            if (viewNavigation == null)
            {
                return;
            }

            viewNavigation.gameObject.SetActive(mode != CameraMode.Inspect);
            overviewView.interactable = mode != CameraMode.Overview;
            deskView.interactable = mode != CameraMode.Desk;
            receptionView.interactable = mode != CameraMode.Window;
            mapView.interactable = mode != CameraMode.Map;
            phoneView.interactable = mode != CameraMode.Phone;
            if (mode == CameraMode.Inspect)
            {
                drawer.SetActive(false);
            }
        }

        private void LateUpdate()
        {
            if (_campaign == null)
            {
                return;
            }

            bool active = _campaign.CanInteract;
            header.SetActive(active);
            footer.SetActive(active);
            bool reading = _rig.Mode == CameraMode.Inspect;
            if (toolStatus != null)
            {
                toolStatus.SetActive(active && (_rig.Mode == CameraMode.Desk || reading) && _queue.State == IntakeState.Received);
            }

            if (!active)
            {
                drawer.SetActive(false);
            }

            actions.gameObject.SetActive(active);
            back.gameObject.SetActive(active && !reading && _rig.Mode != CameraMode.Overview);
            next.gameObject.SetActive(active && !reading && _queue.Remaining > 0 && (_queue.State == IntakeState.Waiting || _queue.State == IntakeState.Filed));
            submit.gameObject.SetActive(active && _rig.Mode == CameraMode.Desk && _review.CanSubmit);
            endShift.gameObject.SetActive(active && !reading && _campaign.CanEndShift);
        }

        private void OnDestroy()
        {
            if (_rig != null)
            {
                _rig.Changed -= ViewChanged;
            }
        }
    }
}
