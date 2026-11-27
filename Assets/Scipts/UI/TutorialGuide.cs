using System;
using System.Linq;
using TMPro;
using UnityEngine.UI;
using UnityEngine;
using Zvezda57.CameraSystem;
using Zvezda57.Core;
using Zvezda57.Dispatch;
using Zvezda57.Documents;
using Zvezda57.Systems;

namespace Zvezda57.UI
{
    public sealed class TutorialGuide : MonoBehaviour
    {
        [SerializeField]
        private GameObject panel;
        [SerializeField]
        private TMP_Text instruction;
        [SerializeField]
        private TMP_Text cue, focusCaption;
        [SerializeField]
        private Button focusButton;
        [SerializeField]
        private Transform arrow;
        [SerializeField]
        private RectTransform screenPointer;
        [SerializeField]
        private Transform folder, paper, stamp, ink, map, repairCrew, sector;
        [SerializeField]
        private RectTransform submitButton, nextButton, endButton, closeReaderButton;
        [SerializeField]
        private RectTransform answerCallButton, confirmVerdictButton;
        [SerializeField]
        private float arrowHeight = .35f;
        private ICampaignPlayState _campaign;
        private ICaseQueue _queue;
        private IReviewProgress _review;
        private IDispatchStatus _dispatch;
        private Func<bool> _isFolderOpen;
        private Func<bool> _isRepairCrewSelected;
        private Func<int, Transform> _sectorTarget;
        private CameraRig _rig;
        private CameraMode _focus;
        private bool _read;
        [SerializeField]
        private Button detailsButton;
        [SerializeField]
        private GameObject detailsPanel;
        [SerializeField]
        private TMP_Text detailsText;
        private string _fullInstruction;
        public void ConfigureCompact(Button button, GameObject panel, TMP_Text text)
        {
            detailsButton = button;
            detailsPanel = panel;
            detailsText = text;
        }

        public string CurrentInstruction => _fullInstruction ?? instruction.text;

        public void Bind(ICampaignPlayState campaign, ICaseQueue queue, IReviewProgress review, IDispatchStatus dispatch,
            CameraRig rig, Func<bool> isFolderOpen, Func<bool> isRepairCrewSelected, Func<int, Transform> sectorTarget)
        {
            _campaign = campaign;
            _queue = queue;
            _review = review;
            _dispatch = dispatch;
            _isFolderOpen = isFolderOpen;
            _isRepairCrewSelected = isRepairCrewSelected;
            _sectorTarget = sectorTarget;
            _rig = rig;
            if (detailsButton != null)
            {
                detailsButton.onClick.RemoveAllListeners();
                detailsButton.onClick.AddListener(() => detailsPanel.SetActive(!detailsPanel.activeSelf));
                detailsPanel.SetActive(false);
            }

            focusButton.onClick.RemoveAllListeners();
            focusButton.onClick.AddListener(() => rig.Go(_focus));
        }

        public void Configure(GameObject panel, TMP_Text text, Button button, Transform arrow, RectTransform pointer, Transform[] world, RectTransform[] controls)
        {
            this.panel = panel;
            instruction = text;
            focusButton = button;
            this.arrow = arrow;
            screenPointer = pointer;
            folder = world[0];
            paper = world[1];
            stamp = world[2];
            ink = world[3];
            map = world[4];
            repairCrew = world[5];
            sector = world[6];
            submitButton = controls[0];
            nextButton = controls[1];
            endButton = controls[2];
            closeReaderButton = controls[3];
        }

        private void LateUpdate()
        {
            if (_campaign == null)
            {
                return;
            }

            bool visible = _campaign.ShiftIndex == 0 && _campaign.CanInteract;
            panel.SetActive(visible);
            arrow.gameObject.SetActive(false);
            screenPointer.gameObject.SetActive(false);
            if (!visible)
            {
                return;
            }

            var (title, action, details, target, control, focus) = CurrentStep();
            _focus = focus;
            RenderStep(title, action, details);

            bool showFocus = target != null && _rig.Mode != focus;
            focusButton.gameObject.SetActive(showFocus);
            if (showFocus && focusCaption != null)
            {
                focusCaption.text = focus == CameraMode.Map ? "К карте" : "К столу";
            }

            if (target != null && _rig.Mode == focus && !_rig.IsMoving)
            {
                arrow.gameObject.SetActive(true);
                float height = focus == CameraMode.Map ? arrowHeight * .5f : arrowHeight;
                float bounce = Mathf.Sin(Time.unscaledTime * 4) * .025f;
                if (focus == CameraMode.Map && target != repairCrew)
                {
                    arrow.position = target.position + new Vector3(.25f + bounce, .065f, -.08f);
                    arrow.rotation = Quaternion.AngleAxis(-72, Vector3.forward) * Quaternion.Euler(90, 0, 0);
                }
                else
                {
                    arrow.position = target.position + Vector3.up * (height + bounce);
                    arrow.rotation = Quaternion.Euler(90, 0, 0);
                }
            }

            if (control != null && control.gameObject.activeInHierarchy)
            {
                screenPointer.gameObject.SetActive(true);
                bool pointFromLeft = control.position.x > Screen.width * .75f;
                float offset = 20 + Mathf.Sin(Time.unscaledTime * 5) * 5;
                float x = pointFromLeft ? control.rect.xMin - offset : control.rect.xMax + offset;
                screenPointer.position = control.TransformPoint(new Vector3(x, control.rect.center.y, 0));
                screenPointer.localRotation = Quaternion.Euler(0, 0, pointFromLeft ? 180 : 0);
            }
        }

        private (string Title, string Action, string Details, Transform Target, RectTransform Control, CameraMode Focus) CurrentStep()
        {
            if (_campaign.CallState is IncomingCallState.Ringing or IncomingCallState.Missed)
            {
                return ("Звонок руководства", "Нажмите «Ответить» справа.", "При пропущенном звонке можно перезвонить. Во время разговора время остановится.",
                    null, answerCallButton, _rig.Mode);
            }

            if (_queue.Current?.Id == "0.1" && _queue.State == IntakeState.Offered)
            {
                return ("1/7 · Возьмите папку", "Щёлкните по папке слева на столе.", "Кнопка «К столу» приблизит рабочее место. Папку оставил Юра.",
                    folder, null, CameraMode.Desk);
            }

            if (_queue.Current?.Id == "0.1" && _queue.State == IntakeState.Received)
            {
                return DocumentStep();
            }

            var incident = _dispatch.Incidents.FirstOrDefault(item => item.State == IncidentState.Waiting);
            if (incident != null)
            {
                if (_rig.Mode != CameraMode.Map)
                {
                    return ("Учебный вызов", "Откройте карту диспетчерской.", "Красный сигнал означает, что вызов ждёт экипаж.", map, null, CameraMode.Map);
                }

                if (!_isRepairCrewSelected())
                {
                    return ("Учебный вызов", "Щёлкните по штекеру «Ремонтники».", "Свободный экипаж отмечен подписью. Выбранный штекер можно отменить повторным щелчком.",
                        repairCrew, null, CameraMode.Map);
                }

                return ($"Учебный вызов · сектор {incident.Sector}", $"Щёлкните по горящему сектору {incident.Sector}.",
                    "Когда экипаж выедет, лампа сменит цвет. Штекер также можно перетащить на сектор.",
                    _sectorTarget(incident.Sector) ?? sector, null, CameraMode.Map);
            }

            if (_campaign.CanEndShift)
            {
                return ("Обучение завершено", "Нажмите «Завершить смену» внизу.", "В сводке можно посмотреть последствия своих решений.",
                    null, endButton, _rig.Mode);
            }

            if (_queue.Remaining > 0 && _queue.State is IntakeState.Filed or IntakeState.Waiting)
            {
                return ("Приём посетителей", "Нажмите «Вызвать посетителя».", "Посетитель оставит папку в приёмном лотке.",
                    null, nextButton, _rig.Mode);
            }

            return ("Учебный вызов", "Дождитесь, когда ремонтники вернутся.", "Статус экипажа виден на карте. После возвращения можно завершить смену.",
                null, null, _rig.Mode);
        }

        private (string Title, string Action, string Details, Transform Target, RectTransform Control, CameraMode Focus) DocumentStep()
        {
            if (!_isFolderOpen())
            {
                return ("2/7 · Откройте папку", "Щёлкните по папке ещё раз.", "На столе появятся учебное дело и памятка.",
                    folder, null, CameraMode.Desk);
            }

            if (_rig.Mode == CameraMode.Inspect)
            {
                _read = true;
                return ("3/7 · Прочитайте лист", "Прочитайте данные и нажмите «Закрыть».", "«Следующий» открывает памятку. Колёсико меняет размер текста.",
                    null, closeReaderButton, CameraMode.Inspect);
            }

            if (_review.CanSubmit)
            {
                if (_rig.Mode != CameraMode.Desk)
                {
                    return ("7/7 · Сдайте дело", "Вернитесь к столу.", "Кнопка «Сдать дело» появится внизу, когда вы откроете ракурс стола.",
                        paper, null, CameraMode.Desk);
                }

                if (confirmVerdictButton != null && confirmVerdictButton.gameObject.activeInHierarchy)
                {
                    return ("7/7 · Подтвердите решение", "Нажмите «Подтвердить».", "До подтверждения можно вернуться и поставить другой штамп.",
                        null, confirmVerdictButton, CameraMode.Desk);
                }

                return ("7/7 · Сдайте дело", "Нажмите «Сдать дело» внизу.", "Решение попадёт в журнал после подтверждения.",
                    null, submitButton, CameraMode.Desk);
            }

            if (_review.SelectedStamp.HasValue)
            {
                return _review.Inked
                    ? ("6/7 · Поставьте оттиск", "Щёлкните по учебному листу.", "Один оттиск задаёт решение по всей папке.", paper, null, CameraMode.Desk)
                    : ("5/7 · Смочите штамп", "Щёлкните по открытой подушке справа.", "Подушка нужна перед каждым оттиском.", ink, null, CameraMode.Desk);
            }

            return _read
                ? ("4/7 · Возьмите штамп", "Щёлкните по штампу «Разрешить».", "В учебном деле реквизиты совпадают. «Разрешить» — первый штамп слева.", stamp, null, CameraMode.Desk)
                : ("3/7 · Откройте лист", "Щёлкните по учебному листу в центре.", "Короткий щелчок открывает лист. Удерживайте ЛКМ, чтобы переместить его.", paper, null, CameraMode.Desk);
        }

        private void RenderStep(string title, string action, string details)
        {
            string text = title + "\n" + action + "\n" + details;
            if (_fullInstruction == text)
            {
                return;
            }

            _fullInstruction = text;
            if (cue != null)
            {
                cue.text = action;
            }

            instruction.text = cue == null && detailsPanel == null ? title + "\n" + action : title;
            if (detailsPanel != null)
            {
                detailsPanel.SetActive(false);
                detailsText.text = cue == null ? action + "\n" + details : details;
            }
        }
    }
}
