using System;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Zvezda57.CameraSystem;
using Zvezda57.Core;
using Zvezda57.Dispatch;
using Zvezda57.InputSystem;
using Zvezda57.Systems;

namespace Zvezda57.UI
{
    public sealed class CampaignHud : MonoBehaviour
    {
        [SerializeField]
        private Transform root;
        [SerializeField]
        private OperationsHud operations;
        [SerializeField]
        private GameObject modal, callBanner, pauseInstructions, dialogueInstructions;
        [SerializeField]
        private TMP_Text status, callText, dialogueText, radioPrompt, radioWarning, radioClock, summary, ending;
        [SerializeField]
        private Button endShiftButton, archiveButton, courierButton, pauseButton, answerButton, menuButton, modalPauseButton,
            resumeButton, saveButton, cancelRadioButton;
        [SerializeField]
        private LabeledButton resultAction, resultProceed;
        [SerializeField]
        private LabeledButton[] dialogueChoices, radioOptions;

        private ICampaignService _campaign;
        private ICityMetrics _metrics;
        private IDispatchStatus _dispatch;
        private MouseInteractor _mouse;
        private CameraRig _rig;
        private Action _restart;
        private CampaignPhase _rendered = CampaignPhase.Menu;
        private string _dialogueId;

        internal int DialogueCapacity => dialogueChoices?.Length ?? 0;
        internal int RadioCapacity => radioOptions?.Length ?? 0;

        public void Build(DeskHud hud, ICampaignService campaign, ICityMetrics metrics, MouseInteractor mouse, CameraRig rig, Action restart, IDispatchStatus dispatch)
        {
            _campaign = campaign;
            _metrics = metrics;
            _dispatch = dispatch;
            _mouse = mouse;
            _rig = rig;
            _restart = restart;

            endShiftButton.onClick.AddListener(() =>
            {
                if (!_campaign.EndShift())
                {
                    hud.SetHint("Завершите дела, вызовы, поручения и дождитесь возвращения служб.");
                }
            });
            archiveButton.onClick.AddListener(() => Assign(YuraTask.Archive, hud));
            courierButton.onClick.AddListener(() => Assign(YuraTask.Courier, hud));
            pauseButton.onClick.AddListener(() =>
            {
                _mouse.CancelDrag();
                _campaign.TogglePause();
            });
            answerButton.onClick.AddListener(() => _campaign.AnswerCall());
            menuButton.onClick.AddListener(SceneNavigation.OpenMenu);
            modalPauseButton.onClick.AddListener(() => _campaign.TogglePause());
            resumeButton.onClick.AddListener(() => _campaign.TogglePause());
            saveButton.onClick.AddListener(() =>
            {
                _campaign.Checkpoint();
                status.text = "Игра сохранена";
            });
            cancelRadioButton.onClick.AddListener(() => _campaign.CancelRadio());
            resultAction.Bind(ResultAction);
            resultProceed.Bind(ResultProceed);

            for (int i = 0; i < dialogueChoices.Length; i++)
            {
                int choice = i;
                dialogueChoices[i].Bind(() => _campaign.Choose(choice));
            }

            for (int i = 0; i < radioOptions.Length; i++)
            {
                int option = i;
                radioOptions[i].Bind(() => _campaign.ChooseRadio(option));
            }

            callBanner.SetActive(false);
            modal.SetActive(false);
            root.gameObject.SetActive(false);
            _campaign.Changed += Render;
        }

        public void Show()
        {
            root.gameObject.SetActive(true);
        }

        private void Assign(YuraTask task, DeskHud hud)
        {
            _campaign.Assign(task, out var message);
            hud.SetHint(message);
        }

        private void Render()
        {
            status.text = _campaign.Shift.Title + (_campaign.Task == YuraTask.None ? "" : " · Юра: " + Mathf.CeilToInt(_campaign.TaskRemaining) + " с");
            callBanner.SetActive(_campaign.CallState == IncomingCallState.Ringing || _campaign.CallState == IncomingCallState.Missed);
            callText.text = _campaign.CallState == IncomingCallState.Ringing ? "Звонок · " + Mathf.CeilToInt(_campaign.CallRemaining) + " с" : "Пропущенный звонок";

            var phase = _campaign.Phase;
            if (phase == CampaignPhase.Radio)
            {
                radioClock.text = $"Эфир: {Mathf.CeilToInt(_campaign.RadioRemaining)} с · Ошибок: {_campaign.RadioMistakes}/{_campaign.RadioMistakeLimit}";
                radioWarning.text = "Вызовы продолжаются: " + string.Join("; ", _dispatch.Incidents.Where(x => x.IsActive).Select(x => "С-" + x.Sector + " " + x.StageTitle + " · " + Mathf.CeilToInt(x.Remaining) + " с"));
            }

            string nextId = phase == CampaignPhase.Radio ? "radio:" + _campaign.RadioIndex + ":" + _campaign.RadioMistakes : _campaign.Dialogue?.Id;
            if (_rendered == phase && _dialogueId == nextId)
            {
                return;
            }

            bool blocked = !_campaign.CanInteract;
            _mouse.Blocked = blocked || _rig.Mode == CameraMode.Inspect;
            _rendered = phase;
            _dialogueId = nextId;
            modal.SetActive(blocked);
            if (!blocked)
            {
                return;
            }

            _mouse.CancelDrag();
            ShowModalContent(phase);
            root.SetAsLastSibling();
        }

        private void ShowModalContent(CampaignPhase phase)
        {
            bool paused = phase == CampaignPhase.Paused;
            bool dialogue = phase == CampaignPhase.Dialogue && _campaign.Dialogue != null;
            bool radio = phase == CampaignPhase.Radio;
            bool showSummary = phase == CampaignPhase.Summary;
            bool showEnding = phase == CampaignPhase.Ending;
            modalPauseButton.gameObject.SetActive(!paused && !showSummary && !showEnding);
            pauseInstructions.SetActive(paused);
            resumeButton.gameObject.SetActive(paused);
            saveButton.gameObject.SetActive(paused);
            dialogueText.gameObject.SetActive(dialogue);
            dialogueInstructions.SetActive(dialogue);
            radioPrompt.gameObject.SetActive(radio);
            radioWarning.gameObject.SetActive(radio);
            radioClock.gameObject.SetActive(radio);
            cancelRadioButton.gameObject.SetActive(radio);
            summary.gameObject.SetActive(showSummary);
            ending.gameObject.SetActive(showEnding);
            resultAction.Show(showSummary || showEnding);
            resultProceed.Show(showSummary || showEnding);

            ShowChoices(dialogue);
            ShowRadioOptions(radio);
            if (showSummary)
            {
                summary.text = "СВОДКА · " + _campaign.Shift.Title + "\n\n" + $"Реактор {_metrics.Reactor}/100 · Секретность {_metrics.Secrecy}/100 · Спокойствие {_metrics.Calm}/100\nДоверие Юры: {_campaign.YuraTrust} · Начислено за смену: {_campaign.Shift.Reward}\n\n" + string.Join("\n", _metrics.History.Reverse().Take(5));
                resultAction.SetCaption("Распределить резерв");
                resultProceed.SetCaption("Продолжить");
            }
            else if (showEnding)
            {
                ending.text = _campaign.Ending.Title + "\n\n" + _campaign.Ending.Text + "\n\n" + $"Реактор {_metrics.Reactor} · Секретность {_metrics.Secrecy} · Спокойствие {_metrics.Calm}\nДоверие Юры: {_campaign.YuraTrust}";
                resultAction.SetCaption("Начать заново");
                resultProceed.SetCaption("Выход");
            }
        }

        private void ShowChoices(bool visible)
        {
            var dialogue = visible ? _campaign.Dialogue : null;
            if (dialogue != null)
            {
                dialogueText.text = dialogue.Speaker + "\n\n" + dialogue.Text;
            }

            for (int i = 0; i < dialogueChoices.Length; i++)
            {
                var button = dialogueChoices[i];
                bool available = dialogue != null && i < dialogue.Choices.Length;
                button.Show(available);
                if (available)
                {
                    var choice = dialogue.Choices[i];
                    button.SetCaption(choice.Text);
                    button.Interactable = _campaign.CanChoose(i);
                }
            }
        }

        private void ShowRadioOptions(bool visible)
        {
            var step = visible ? _campaign.RadioStep : null;
            if (visible)
            {
                radioPrompt.text = "РАДИОИГРА · Юра\n\n" + (step?.Prompt ?? "Последовательность завершена");
            }

            for (int i = 0; i < radioOptions.Length; i++)
            {
                bool available = step != null && i < step.Options.Length;
                radioOptions[i].Show(available);
                if (available)
                {
                    radioOptions[i].SetCaption(step.Options[i]);
                }
            }
        }

        private void ResultAction()
        {
            if (_campaign.Phase == CampaignPhase.Summary)
            {
                operations.OpenUpgrades();
            }
            else if (_campaign.Phase == CampaignPhase.Ending)
            {
                _restart();
            }
        }

        private void ResultProceed()
        {
            if (_campaign.Phase == CampaignPhase.Summary)
            {
                _campaign.Continue();
            }
            else if (_campaign.Phase == CampaignPhase.Ending)
            {
                Application.Quit();
            }
        }

        private void OnDestroy()
        {
            if (_campaign != null)
            {
                _campaign.Changed -= Render;
            }
        }
    }
}
