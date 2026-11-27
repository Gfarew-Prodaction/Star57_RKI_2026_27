using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Zvezda57.Core;
using Zvezda57.Dispatch;
using Zvezda57.Documents;

namespace Zvezda57.Systems
{
    public sealed class CampaignService : ICampaignService, IDisposable
    {
        private const float MaximumTickStep = .1f;
        private const float AutosaveInterval = 5f;
        private const string RadioGameFlag = "RadioGame";

        private readonly CampaignDefinition _definition;
        private readonly IReadOnlyDictionary<string, CaseDefinition> _cases;
        private readonly ICaseQueue _queue;
        private readonly IDecisionStore _journal;
        private readonly IDispatchService _dispatch;
        private readonly ICityMetrics _metrics;
        private readonly IUpgradeService _upgrades;
        private readonly RadioChallenge _radio;
        private readonly JsonSaveFile<CampaignSave> _file;
        private readonly CampaignSave _save;
        private bool _started, _inTick, _dirty;
        private float _autosave;
        private readonly IReviewService _review;
        private Func<DeskSnapshot> _captureView;
        public DeskSnapshot Desk => _save.Desk;

        public void BindView(Func<DeskSnapshot> capture)
        {
            _captureView = capture;
        }

        public IncomingCallState CallState => _save.CallState;
        public float CallRemaining => _save.CallRemaining;
        public bool TaskInterrupted => Task != YuraTask.None && (!CanInteract || CallState == IncomingCallState.Ringing);
        public RadioStep RadioStep => _radio.Current;
        public int RadioIndex => _radio.Index;
        public float RadioRemaining => _radio.Remaining;
        public int RadioMistakes => _radio.Mistakes;
        public int RadioMistakeLimit => RadioChallenge.MistakeLimit;
        public CampaignPhase Phase => _started ? _save.Phase : CampaignPhase.Menu;
        public ShiftDefinition Shift => _definition.Shifts[_save.ShiftIndex];
        public int ShiftIndex => _save.ShiftIndex;
        private bool IsFinalShift => ShiftIndex == _definition.Shifts.Length - 1;
        public int YuraTrust => _save.YuraTrust;
        public YuraTask Task => _save.Task;
        public float TaskRemaining => _save.TaskRemaining;
        public bool CanInteract => Phase == CampaignPhase.Running;
        public DialogueDefinition Dialogue => AllDialogues.FirstOrDefault(x => x.Id == _save.PendingDialogue);
        public EndingDefinition Ending => _definition.Endings.FirstOrDefault(x => x.Kind == _save.Ending);
        private IEnumerable<DialogueDefinition> AllDialogues => _definition.Story.Concat(_definition.Shifts.Select(x => x.Intro));
        private bool CasesDone => Shift.CaseIds.All(id => _journal.Records.Any(x => x.CaseId == id));
        public bool CanEndShift =>
            CanInteract &&
            CasesDone &&
            Task == YuraTask.None &&
            (ShiftIndex != 0 || HasFlag("Briefed")) &&
            Shift.Incidents.All(planned => _save.Incidents.Any(result => result.Id == planned.IncidentId)) &&
            _dispatch.Incidents.All(incident => !incident.IsActive) &&
            _dispatch.Crews.All(crew => crew.State == CrewState.Free) &&
            (!IsFinalShift || _save.FinalTriggered);

        public event Action Changed;
        public CampaignService(CampaignDefinition definition, CaseDefinition[] cases, ICaseQueue queue, IDecisionStore journal, IDispatchService dispatch, ICityMetrics metrics, IUpgradeService upgrades, string path, IReviewService review)
        {
            _review = review;
            _definition = definition;
            _cases = cases.ToDictionary(item => item.Id);
            _queue = queue;
            _journal = journal;
            _dispatch = dispatch;
            _metrics = metrics;
            _upgrades = upgrades;
            _file = new JsonSaveFile<CampaignSave>(path, "campaign progress");
            var loaded = _file.Load(saved => saved.Version == 1 &&
                saved.Flags != null && saved.Incidents != null && saved.SeenDialogues != null);
            _save = loaded ?? new CampaignSave();
            _radio = new RadioChallenge(definition.RadioSteps, definition.RadioSeconds);
            _radio.Restore(_save.RadioIndex, _save.RadioMistakes, _save.RadioRemaining);
            if (_save.ShiftIndex < 0 || _save.ShiftIndex >= definition.Shifts.Length)
            {
                throw new InvalidDataException("Invalid saved shift index");
            }

            bool hasSavedMetrics = _save.Metrics != null;
            if (hasSavedMetrics)
            {
                metrics.Restore(_save.Metrics);
            }

            foreach (var record in journal.Records)
            {
                RestoreCaseDecision(record, loaded == null || hasSavedMetrics);
            }

            foreach (var result in _save.Incidents)
            {
                var incident = dispatch.Incidents.FirstOrDefault(x => x.Id == result.Id);
                if (incident != null)
                {
                    metrics.Apply("incident:" + result.Id, incident.Title, incident.GetEffect(result.Success));
                }
            }

            dispatch.Finished += IncidentFinished;
            upgrades.PurchasingAllowed = false;
        }

        public void Checkpoint()
        {
            if (_started)
            {
                Persist();
            }
        }

        private void Persist()
        {
            if (_inTick)
            {
                _dirty = true;
                return;
            }

            if (_started)
            {
                _save.Queue = _queue.Capture();
                _save.Dispatch = _dispatch.Capture();
                _save.Review = _review.Capture();

                if (_captureView != null)
                {
                    _save.Desk = _captureView();
                }
            }

            _save.Metrics = _metrics.Capture();
            _save.RadioIndex = _radio.Index;
            _save.RadioMistakes = _radio.Mistakes;
            _save.RadioRemaining = _radio.Remaining;
            _file.Write(_save);
        }

        public bool HasFlag(string flag) => _save.Flags.Contains(flag);
        private void Flag(string flag)
        {
            if (!string.IsNullOrEmpty(flag) && !HasFlag(flag))
            {
                _save.Flags.Add(flag);
            }
        }

        public void Start()
        {
            if (_started)
            {
                return;
            }

            _started = true;
            var savedQueue = _save.Queue;
            var savedReview = _save.Review;
            var savedDispatch = _save.Dispatch;
            if (savedQueue?.State == IntakeState.Received && _journal.Records.Any(record => record.CaseId == savedQueue.CaseId))
            {
                savedQueue = new QueueSnapshot { CaseId = savedQueue.CaseId, State = IntakeState.Filed };
                savedReview = null;
            }

            ConfigureShift();
            _review.ToolsUnlocked = ShiftIndex >= 2;
            _queue.Restore(savedQueue);
            _review.Restore(savedReview);
            _dispatch.Restore(savedDispatch);
            RestoreCaseIncidents();
            if (_save.Phase == CampaignPhase.Menu)
            {
                ShowDialogue(Shift.Intro);
            }
            else if (_save.Phase == CampaignPhase.Running)
            {
                _dispatch.Start();
                Offer();
            }
            else if (_save.Phase == CampaignPhase.Paused || _save.Phase == CampaignPhase.Radio || _save.Phase == CampaignPhase.Dialogue)
            {
                _dispatch.Start();
            }
            else if (_save.Phase == CampaignPhase.Summary)
            {
                _upgrades.PurchasingAllowed = true;
            }

            Changed?.Invoke();
        }

        private void ConfigureShift()
        {
            _queue.SetCases(Shift.CaseIds.Select(id => _cases[id]).Where(x => !_journal.Records.Any(y => y.CaseId == x.Id)).ToArray());
            _dispatch.ConfigureSchedule(Shift.Incidents.ToDictionary(x => x.IncidentId, x => x.Delay), _save.Incidents.ToDictionary(x => x.Id, x => x.Success));
        }

        private void RestoreCaseIncidents()
        {
            foreach (var record in _journal.Records.Where(record => Shift.CaseIds.Contains(record.CaseId)))
            {
                TriggerCaseIncident(record);
            }

            if (_save.FinalTriggered && !_save.Incidents.Any(x => x.Id == _definition.FinaleIncidentId))
            {
                _dispatch.Trigger(_definition.FinaleIncidentId);
            }
        }

        private void Offer()
        {
            if (_queue.State == IntakeState.Waiting)
            {
                _queue.OfferNext();
            }
        }

        private void ShowDialogue(DialogueDefinition dialogue)
        {
            _save.Phase = CampaignPhase.Dialogue;
            _save.PendingDialogue = dialogue.Id;
            Persist();
            Changed?.Invoke();
        }

        private bool Story(string id)
        {
            var dialogue = _definition.Story.FirstOrDefault(x => x.Id == id);
            if (dialogue == null || _save.SeenDialogues.Contains(id))
            {
                return false;
            }

            ShowDialogue(dialogue);
            return true;
        }

        public bool CanChoose(int index) => GetAvailableChoice(Dialogue, index) != null;

        private DialogueChoice GetAvailableChoice(DialogueDefinition dialogue, int index)
        {
            if (Phase != CampaignPhase.Dialogue || dialogue == null || index < 0 || index >= dialogue.Choices.Length)
            {
                return null;
            }

            var choice = dialogue.Choices[index];
            bool available = YuraTrust >= choice.MinimumTrust &&
                (string.IsNullOrEmpty(choice.RequiredFlag) || HasFlag(choice.RequiredFlag));
            return available ? choice : null;
        }

        public bool Choose(int index)
        {
            var dialogue = Dialogue;
            var choice = GetAvailableChoice(dialogue, index);
            if (choice == null)
            {
                return false;
            }

            _save.YuraTrust += choice.TrustDelta;
            if (!_save.SeenDialogues.Contains(dialogue.Id))
            {
                _save.SeenDialogues.Add(dialogue.Id);
            }

            _save.PendingDialogue = null;
            if (choice.SetFlag == RadioGameFlag)
            {
                _save.Phase = CampaignPhase.Radio;
                _radio.Start();
            }
            else
            {
                Flag(choice.SetFlag);
                _save.Phase = CampaignPhase.Running;
            }

            _dispatch.Start();
            Offer();
            Persist();
            Changed?.Invoke();
            return true;
        }

        public DecisionRecord SubmitCase()
        {
            if (!CanInteract || _review.Submit() is not DecisionRecord record)
            {
                return null;
            }

            ApplyCaseDecision(record, record.Metrics, updateTrust: true);
            TriggerCaseIncident(record);
            Persist();
            Changed?.Invoke();
            return record;
        }

        private void RestoreCaseDecision(DecisionRecord record, bool updateTrust)
        {
            var delta = record.Metrics;
            if (delta.Reactor == 0 && delta.Secrecy == 0 && delta.Calm == 0 &&
                _cases.TryGetValue(record.CaseId, out var definition))
            {
                var outcome = definition.Verdicts.FirstOrDefault(rule => rule.Verdict == record.Verdict);
                if (outcome != null)
                {
                    delta = outcome.Metrics;
                }
            }

            ApplyCaseDecision(record, delta, updateTrust);
        }

        private void ApplyCaseDecision(DecisionRecord record, MetricDelta delta, bool updateTrust)
        {
            if (_metrics.Apply("case:" + record.CaseId, "Дело " + record.CaseId + ": " + record.Feedback, delta))
            {
                ApplyCaseStory(record, updateTrust);
            }

            if (record.CaseId == "0.1" && ShiftIndex == 0 && CallState == IncomingCallState.None &&
                !_save.SeenDialogues.Contains("D0_PHONE"))
            {
                _save.CallState = IncomingCallState.Ringing;
                _save.CallDialogue = "D0_PHONE";
                _save.CallRemaining = _definition.IncomingCallSeconds;
            }
        }

        private void ApplyCaseStory(DecisionRecord record, bool updateTrust)
        {
            var evidenceIds = record.Evidence ?? Array.Empty<string>();
            foreach (var evidence in evidenceIds)
            {
                switch (evidence)
                {
                    case "magnifier_3.2":
                        Flag("TypewriterMark");
                        break;
                    case "uv_3.2":
                        Flag("RadioClue");
                        break;
                }
            }

            if (updateTrust && record.CaseId == "2.2")
            {
                _save.YuraTrust += record.Verdict == CaseVerdict.Detain ? -2 : 1;
            }

            if (record.CaseId == "1.2" && evidenceIds.Contains("archive_1.2"))
            {
                Flag("VetrovAlerted");
            }
        }

        private void TriggerCaseIncident(DecisionRecord record)
        {
            if (!string.IsNullOrEmpty(record.IncidentId))
            {
                _dispatch.Trigger(record.IncidentId);
            }
        }

        private void IncidentFinished(IIncidentStatus incident)
        {
            if (_save.Incidents.Any(x => x.Id == incident.Id))
            {
                return;
            }

            bool success = incident.State == IncidentState.Resolved;
            _metrics.Apply("incident:" + incident.Id, incident.Title + (success ? " — устранено" : " — провалено"), incident.GetEffect(success));
            _save.Incidents.Add(new IncidentResult { Id = incident.Id, Success = success });
            if (incident.Id == _definition.CriticalIncidentId && !success)
            {
                Flag("CriticalFailed");
            }

            if (incident.Id == _definition.FinaleIncidentId && success)
            {
                Flag("Intercepted");
            }

            Persist();
            Changed?.Invoke();
        }

        public bool Assign(YuraTask task, out string message)
        {
            if (!CanInteract || task == YuraTask.None || _save.Task != YuraTask.None)
            {
                message = "Юра занят или поручение сейчас недоступно.";
                return false;
            }

            if (task == YuraTask.Courier && _save.ShiftIndex < 2)
            {
                message = "Слежка за курьером доступна со второй смены.";
                return false;
            }

            _save.Task = task;
            _save.TaskRemaining = _definition.YuraTaskSeconds;
            message = "Юра принял поручение.";
            Persist();
            Changed?.Invoke();
            return true;
        }

        public void Tick(float seconds)
        {
            if (seconds <= 0 || float.IsNaN(seconds) || float.IsInfinity(seconds))
            {
                return;
            }

            while (seconds > 0 && (CanInteract || Phase == CampaignPhase.Radio))
            {
                float step = Math.Min(MaximumTickStep, seconds);
                seconds -= step;
                TickSlice(step);
            }
        }

        private void TickSlice(float seconds)
        {
            _inTick = true;
            try
            {
                if (Phase == CampaignPhase.Radio)
                {
                    TickRadio(seconds);
                }
                else
                {
                    TickRunning(seconds);
                }
            }
            finally
            {
                _inTick = false;
                _autosave += seconds;
                if (_dirty || _autosave >= AutosaveInterval)
                {
                    _dirty = false;
                    _autosave = 0;
                    Persist();
                }
            }
        }

        private void TickRadio(float seconds)
        {
            _dispatch.Tick(seconds);
            if (_radio.Elapse(seconds))
            {
                CancelRadio();
            }

            Changed?.Invoke();
        }

        private void TickRunning(float seconds)
        {
            _dispatch.Tick(seconds);
            TickIncomingCall(seconds);
            TickYuraAssignment(seconds);
            if (FinaleIsReady)
            {
                if (ShouldShowFinaleDialogue)
                {
                    Story("D3_YURA");
                    return;
                }

                TriggerFinaleIncident();
            }

            Changed?.Invoke();
        }

        private void TickIncomingCall(float seconds)
        {
            if (_save.CallState == IncomingCallState.Ringing)
            {
                _save.CallRemaining = Math.Max(0, _save.CallRemaining - seconds);
                if (_save.CallRemaining <= 0)
                {
                    _save.CallState = IncomingCallState.Missed;
                    _metrics.Apply("call:missed:" + _save.CallDialogue, "Пропущен звонок руководства", new MetricDelta(0, 0, -3));
                    Persist();
                }
            }
        }

        private void TickYuraAssignment(float seconds)
        {
            if (_save.Task != YuraTask.None && _save.CallState != IncomingCallState.Ringing)
            {
                _save.TaskRemaining = Math.Max(0, _save.TaskRemaining - seconds);
                if (_save.TaskRemaining <= 0)
                {
                    bool courier = _save.Task == YuraTask.Courier;
                    Flag(courier ? "CourierTracked" : "ArchiveChecked");
                    _save.Task = YuraTask.None;
                    Persist();
                    Story(courier ? "YURA_COURIER" : "YURA_ARCHIVE");
                }
            }
        }

        private bool FinaleIsReady =>
            IsFinalShift && CasesDone && !_save.FinalTriggered && CanInteract;

        private bool ShouldShowFinaleDialogue =>
            HasFlag("TypewriterMark") && HasFlag("RadioClue") && !_save.SeenDialogues.Contains("D3_YURA");

        private void TriggerFinaleIncident()
        {
            _save.FinalTriggered = true;
            if (!HasFlag(RadioGameFlag) && !HasFlag("CourierTracked"))
            {
                Flag("NoInterceptLead");
            }

            _dispatch.Trigger(_definition.FinaleIncidentId);
            Persist();
        }

        public bool EndShift()
        {
            if (!CanEndShift)
            {
                return false;
            }

            _save.Phase = CampaignPhase.Summary;
            _upgrades.PurchasingAllowed = true;
            _upgrades.Reward("shift:" + _save.ShiftIndex, Shift.Reward);
            Persist();
            Changed?.Invoke();
            return true;
        }

        public static EndingKind Evaluate(bool intercepted, bool lead, int reactor, int secrecy, int calm, int threshold)
        {
            if (!intercepted || !lead || reactor <= 0 || secrecy <= 0 || calm <= 0)
            {
                return EndingKind.Bad;
            }

            return reactor >= threshold ? EndingKind.Good : EndingKind.Mixed;
        }

        public bool Continue()
        {
            if (Phase != CampaignPhase.Summary)
            {
                return false;
            }

            _upgrades.Cancel();
            _upgrades.PurchasingAllowed = false;
            if (IsFinalShift)
            {
                _save.Ending = Evaluate(HasFlag("Intercepted"), (HasFlag(RadioGameFlag) || HasFlag("CourierTracked")) && !HasFlag("CriticalFailed"), _metrics.Reactor, _metrics.Secrecy, _metrics.Calm, _definition.ReactorThreshold);
                _save.Phase = CampaignPhase.Ending;
                Persist();
                Changed?.Invoke();
                return true;
            }

            _save.ShiftIndex++;
            _save.Task = YuraTask.None;
            _save.Queue = null;
            _save.Review = null;
            _save.Dispatch = null;
            _save.Desk = null;
            ConfigureShift();
            RestoreCaseIncidents();
            _review.Reset();
            _review.ToolsUnlocked = ShiftIndex >= 2;

            ShowDialogue(Shift.Intro);
            return true;
        }

        public void TogglePause()
        {
            var phase = Phase;
            if (phase is CampaignPhase.Menu or CampaignPhase.Ending or CampaignPhase.Summary)
            {
                return;
            }

            if (phase == CampaignPhase.Paused)
            {
                _save.Phase = _save.ResumePhase;
            }
            else
            {
                _save.ResumePhase = phase;
                _save.Phase = CampaignPhase.Paused;
            }

            Persist();
            Changed?.Invoke();
        }

        public bool AnswerCall()
        {
            if (!CanInteract || (_save.CallState != IncomingCallState.Ringing && _save.CallState != IncomingCallState.Missed))
            {
                return false;
            }

            var dialogue = AllDialogues.FirstOrDefault(x => x.Id == _save.CallDialogue);
            if (dialogue == null)
            {
                return false;
            }

            _save.CallState = IncomingCallState.Answered;
            ShowDialogue(dialogue);
            return true;
        }

        public bool ChooseRadio(int option)
        {
            if (Phase != CampaignPhase.Radio)
            {
                return false;
            }

            var result = _radio.Choose(option);
            if (result == RadioChoiceResult.Invalid)
            {
                return false;
            }

            if (result == RadioChoiceResult.Failed)
            {
                CancelRadio();
                return false;
            }

            if (result == RadioChoiceResult.Completed)
            {
                Flag(RadioGameFlag);
                _save.Phase = CampaignPhase.Running;
            }

            Persist();
            Changed?.Invoke();
            return true;
        }

        public void CancelRadio()
        {
            if (Phase != CampaignPhase.Radio)
            {
                return;
            }

            Flag("RadioFailed");
            _save.Phase = CampaignPhase.Running;
            Persist();
            Changed?.Invoke();
        }

        public void Clear()
        {
            _file.Delete();
        }

        public void Dispose()
        {
            _dispatch.Finished -= IncidentFinished;
        }
    }
}
