using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Zvezda57.CameraSystem;
using Zvezda57.Core;
using Zvezda57.Dispatch;
using Zvezda57.Documents;
using Zvezda57.Systems;

namespace Zvezda57.Editor
{
    public static class RemainingMechanicsValidation
    {
        private static void Require(bool value, string text)
        {
            if (!value)
            {
                throw new Exception(text);
            }
        }

        private static T[] Assets<T>(string folder)
            where T : UnityEngine.Object => AssetDatabase.FindAssets("t:" + typeof(T).Name, new[] { folder }).Select(g => AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(g))).ToArray();
        private sealed class Session : IDisposable
        {
            public readonly JsonDecisionStore Journal;
            public readonly CaseQueue Queue;
            public readonly ReviewService Review;
            public readonly DispatchService Dispatch;
            public readonly CityMetrics Metrics;
            public readonly UpgradeService Upgrades;
            public readonly CampaignService Campaign;
            public Session(string root)
            {
                Journal = new JsonDecisionStore(Path.Combine(root, "decisions"));
                Queue = new CaseQueue(Assets<CaseDefinition>("Assets/Settings/Cases"));
                Review = new ReviewService(Queue, Journal);
                Dispatch = new DispatchService(Assets<IncidentDefinition>("Assets/Settings/Incidents"));
                Metrics = new CityMetrics();
                Upgrades = new UpgradeService(Assets<UpgradeDefinition>("Assets/Settings/Upgrades"), Dispatch, Path.Combine(root, "upgrades"));
                Campaign = new CampaignService(AssetDatabase.LoadAssetAtPath<CampaignDefinition>("Assets/Settings/Shifts/Campaign.asset"), Assets<CaseDefinition>("Assets/Settings/Cases"), Queue, Journal, Dispatch, Metrics, Upgrades, Path.Combine(root, "campaign"), Review);
                Campaign.Start();
            }

            public void Dispose()
            {
                Campaign.Dispose();
            }
        }

        public static void Run()
        {
            if (!Application.isBatchMode)
            {
                throw new InvalidOperationException("Use isolated Editor.");
            }

            SnapshotAndCalls();
            RecoverFiledCase();
            Radio();
            PacedRoutes();
            Debug.Log("Z57_REMAINING_MECHANICS_PASS: exact dispatch/review/desk restore; pause; call ring/miss/recall; Yura interruption/resume; tool locks; radio steps/errors/timeout/restore and courier fallback.");
            OperationsValidation.Run();
        }

        private static void RecoverFiledCase()
        {
            string root = Path.Combine(Path.GetTempPath(), "Z57CaseRecovery-" + Guid.NewGuid());
            Directory.CreateDirectory(root);
            try
            {
                using (var session = new Session(root))
                {
                    Require(session.Campaign.Choose(0) && session.Queue.Receive(), "Tutorial case is received");
                    session.Review.SelectStamp(CaseVerdict.Approve);
                    Require(session.Review.Ink() && session.Review.Stamp(0), "Tutorial case is stamped");
                    session.Campaign.Checkpoint();
                    Require(session.Review.Submit() != null && session.Journal.Records.Count == 1, "Decision reached journal before campaign checkpoint");
                }

                using (var restored = new Session(root))
                {
                    Require(restored.Queue.State == IntakeState.Filed && !restored.Review.CanSubmit,
                        "Recovery does not reopen the filed case or restore its stamp");
                    Require(restored.Campaign.CallState == IncomingCallState.Ringing && restored.Metrics.Reactor == 77,
                        "Recovery restores the story call and metric effect");
                    restored.Campaign.Checkpoint();
                }

                using (var restored = new Session(root))
                {
                    Require(restored.Journal.Records.Count == 1 && restored.Metrics.History.Count == 1 && restored.Metrics.Reactor == 77,
                        "Reloading the recovered case does not duplicate its effects");
                }

                Debug.Log("Z57_CASE_RECOVERY_PASS: journal-only decision restores the filed case, city effect and tutorial call once.");
            }
            finally
            {
                Directory.Delete(root, true);
            }
        }

        private static void PacedRoutes()
        {
            int minimum = int.MaxValue, maximum = 0;
            for (int seed = 0; seed < 12; seed++)
            {
                string root = Path.Combine(Path.GetTempPath(), "Z57Paced-" + Guid.NewGuid());
                Directory.CreateDirectory(root);
                try
                {
                    using (var session = new Session(root))
                    {
                        var c = session.Campaign;
                        var random = new System.Random(seed);
                        string lastCase = null;
                        int caseAge = 0, readBudget = 90, dialogueAge = 0, radioAge = 0, think = 0;
                        int wall = 0;
                        bool pauseDone = false;
                        while (c.Phase != CampaignPhase.Ending && wall++ < 1500)
                        {
                            if (c.Phase == CampaignPhase.Dialogue)
                            {
                                c.Tick(1);
                                if (++dialogueAge < 17)
                                {
                                    continue;
                                }

                                dialogueAge = 0;
                                int index = seed % 2 == 0 && c.Dialogue.Id == "D0_INTRO" ? 1 : 0;
                                if (c.YuraTrust < c.Dialogue.Choices[index].MinimumTrust)
                                {
                                    index = c.Dialogue.Choices.Length - 1;
                                }

                                c.Choose(index);
                                continue;
                            }

                            if (c.Phase == CampaignPhase.Summary)
                            {
                                Require(c.EndShift() == false, "Summary is not reentrant");
                                c.Continue();
                                pauseDone = false;
                                continue;
                            }

                            if (c.Phase == CampaignPhase.Radio)
                            {
                                c.Tick(1);
                                if (++radioAge >= 4)
                                {
                                    radioAge = 0;
                                    c.ChooseRadio(c.RadioStep.CorrectOption);
                                }

                                continue;
                            }

                            if (c.CallState == IncomingCallState.Ringing && c.CallRemaining <= 7 || c.CallState == IncomingCallState.Missed)
                            {
                                c.AnswerCall();
                                continue;
                            }

                            if (!pauseDone && caseAge > 10)
                            {
                                c.TogglePause();
                                c.Tick(30);
                                c.TogglePause();
                                wall += 5;
                                pauseDone = true;
                            }

                            if (c.ShiftIndex >= 2 && c.YuraTrust < 2 && !c.HasFlag("CourierTracked") && c.Task == YuraTask.None)
                            {
                                c.Assign(YuraTask.Courier, out _);
                            }

                            if (session.Queue.State == IntakeState.Waiting || session.Queue.State == IntakeState.Filed)
                            {
                                session.Queue.OfferNext();
                            }

                            if (session.Queue.State == IntakeState.Offered)
                            {
                                session.Queue.Receive();
                                session.Review.Reset();
                            }

                            if (session.Queue.State == IntakeState.Received)
                            {
                                string id = session.Queue.Current.Id;
                                if (id != lastCase)
                                {
                                    lastCase = id;
                                    caseAge = 0;
                                    readBudget = 80 + random.Next(21);
                                }

                                if (++caseAge >= readBudget)
                                {
                                    if (id == "3.2")
                                    {
                                        session.Review.SelectTool(InspectionTool.Magnifier);
                                        session.Review.Scan(0, new Vector2(.5f, .75f));
                                        session.Review.SelectTool(InspectionTool.Ultraviolet);
                                        session.Review.Scan(0, new Vector2(.5f, .75f));
                                    }

                                    session.Review.SelectStamp(session.Queue.Current.Verdicts.First(x => x.Accepted).Verdict);
                                    session.Review.Ink();
                                    session.Review.Stamp(0);
                                    c.SubmitCase();
                                }
                            }

                            if (--think <= 0)
                            {
                                think = 3 + random.Next(4);
                                foreach (var incident in session.Dispatch.Incidents.Where(x => x.State == IncidentState.Waiting).OrderBy(x => x.Remaining).ToArray())
                                {
                                    foreach (var crew in incident.AllowedCrews)
                                    {
                                        if (session.Dispatch.Dispatch(crew, incident.Id, out _))
                                        {
                                            break;
                                        }
                                    }
                                }
                            }

                            c.Tick(1);
                            if (c.CanEndShift)
                            {
                                c.EndShift();
                            }
                        }

                        Require(c.Phase == CampaignPhase.Ending && c.Ending.Kind == EndingKind.Good, "Paced player must finish without a softlock: seed " + seed);
                        Require(session.Journal.Records.Count == 7, "Paced route covers all cases");
                        minimum = Math.Min(minimum, wall);
                        maximum = Math.Max(maximum, wall);
                    }
                }
                finally
                {
                    Directory.Delete(root, true);
                }
            }

            Debug.Log("Z57_PACED_ROUTES_PASS: 12 seeds, 80–100s reading per case, 3–6s dispatch response, pauses, low/high trust; modeled duration " + minimum + "–" + maximum + " seconds. Not a human timing measurement.");
        }

        private static void SnapshotAndCalls()
        {
            string root = Path.Combine(Path.GetTempPath(), "Z57FullSave-" + Guid.NewGuid());
            Directory.CreateDirectory(root);
            try
            {
                float remaining;
                using (var session = new Session(root))
                {
                    var c = session.Campaign;
                    c.Choose(0);
                    session.Queue.Receive();
                    session.Review.SelectTool(InspectionTool.Magnifier);
                    Require(!session.Review.ToolsUnlocked && session.Review.Tool == InspectionTool.None, "Tools locked in tutorial");
                    session.Review.SelectStamp(CaseVerdict.Approve);
                    session.Review.Ink();
                    session.Review.Stamp(0);
                    session.Review.SelectStamp(CaseVerdict.Recheck);
                    session.Review.Ink();
                    Require(c.Assign(YuraTask.Archive, out _) && !c.Assign(YuraTask.Archive, out _), "One active task");
                    c.Tick(3.2f);
                    Require(session.Dispatch.Dispatch(CrewType.Repair, "0.A", out _), "Repair dispatched");
                    c.Tick(.5f);
                    var desk = new DeskSnapshot
                    {
                        CaseId = "0.1",
                        Opened = true,
                        Page = 1,
                        Zoom = 32,
                        Camera = CameraMode.Inspect,
                        PaperPositions = new[]
                        {
                            new Vector3(-.7f, 1.08f, .2f),
                            new Vector3(.8f, 1.08f, -.2f)
                        },
                        ReferenceOpen = true,
                        ReferenceTab = 2,
                        PhoneDigits = "204"
                    };
                    c.BindView(() => desk);
                    remaining = session.Dispatch.Crews[3].Remaining;
                    c.TogglePause();
                    float task = c.TaskRemaining;
                    c.Tick(30);
                    Require(Math.Abs(remaining - session.Dispatch.Crews[3].Remaining) < .001f && Math.Abs(task - c.TaskRemaining) < .001f, "Pause stops all work");
                }

                using (var session = new Session(root))
                {
                    var c = session.Campaign;
                    Require(c.Phase == CampaignPhase.Paused && session.Queue.State == IntakeState.Received && session.Queue.Current.Id == "0.1", "Queue and pause restored");
                    Require(session.Review.GetStamp(0) == CaseVerdict.Approve && session.Review.SelectedStamp == CaseVerdict.Recheck && session.Review.Inked, "Stamps and ink restored");
                    Require(c.Desk.Opened && c.Desk.Page == 1 && c.Desk.Zoom == 32 && c.Desk.PaperPositions[1].x == .8f && c.Desk.PhoneDigits == "204" && c.Desk.ReferenceTab == 2, "Desk and reading state restored");
                    Require(session.Dispatch.Crews[3].State == CrewState.Travel && Math.Abs(session.Dispatch.Crews[3].Remaining - remaining) < .001f, "Exact travel timer restored");
                    c.TogglePause();
                    c.Tick(remaining + .2f);
                    Require(session.Dispatch.Crews[3].State == CrewState.Work, "Restored travel continues");
                    c.Checkpoint();
                }

                using (var session = new Session(root))
                {
                    var c = session.Campaign;
                    Require(session.Dispatch.Crews[3].State == CrewState.Work, "Work stage restored");
                    for (int step = 0; step < 200 && session.Dispatch.Crews[3].State == CrewState.Work; step++)
                    {
                        if (c.Phase == CampaignPhase.Dialogue)
                        {
                            c.Choose(0);
                        }
                        else
                        {
                            c.Tick(.1f);
                        }
                    }

                    Require(session.Dispatch.Crews[3].State == CrewState.Return, "Work proceeds to return");
                    c.Checkpoint();
                }

                using (var session = new Session(root))
                {
                    var c = session.Campaign;
                    Require(session.Dispatch.Crews[3].State == CrewState.Return, "Return stage restored");
                    c.Tick(5);
                    if (c.Task == YuraTask.None)
                    {
                        c.Assign(YuraTask.Archive, out _);
                    }

                    if (c.Phase == CampaignPhase.Dialogue)
                    {
                        c.Choose(0);
                    }

                    c.Assign(YuraTask.Archive, out _);
                    session.Review.SelectStamp(CaseVerdict.Approve);
                    session.Review.Ink();
                    session.Review.Stamp(0);
                    c.SubmitCase();
                    Require(c.CallState == IncomingCallState.Ringing, "Incoming call rings rather than opening dialogue automatically");
                    float task = c.TaskRemaining;
                    c.Tick(2);
                    Require(Math.Abs(task - c.TaskRemaining) < .001f && c.TaskInterrupted, "Priority call suspends Yura");
                    float call = c.CallRemaining;
                    c.TogglePause();
                    c.Tick(50);
                    Require(Math.Abs(call - c.CallRemaining) < .001f, "Pause freezes call deadline");
                    c.TogglePause();
                    c.Tick(call + .1f);
                    Require(c.CallState == IncomingCallState.Missed && c.Phase == CampaignPhase.Running, "Missed call does not block game");
                    Require(c.AnswerCall() && c.Phase == CampaignPhase.Dialogue, "Missed call can be returned");
                    c.Choose(0);
                    Require(c.HasFlag("Briefed"), "Returned call completes mandatory briefing");
                    float after = c.TaskRemaining;
                    c.Tick(.5f);
                    Require(c.TaskRemaining < after || c.Task == YuraTask.None, "Interrupted task resumes");
                }
            }
            finally
            {
                Directory.Delete(root, true);
            }
        }

        private static void Radio()
        {
            string root = Path.Combine(Path.GetTempPath(), "Z57Radio-" + Guid.NewGuid());
            Directory.CreateDirectory(root);
            try
            {
                var state = new CampaignSave
                {
                    ShiftIndex = 3,
                    YuraTrust = 2,
                    Phase = CampaignPhase.Dialogue,
                    PendingDialogue = "D3_YURA"
                };
                state.Flags.AddRange(new[] { "TypewriterMark", "RadioClue" });
                File.WriteAllText(Path.Combine(root, "campaign"), JsonUtility.ToJson(state));
                using (var session = new Session(root))
                {
                    var c = session.Campaign;
                    Require(session.Review.ToolsUnlocked, "Tools unlocked after shift 2");
                    c.Choose(0);
                    Require(c.Phase == CampaignPhase.Radio && !c.HasFlag("RadioGame"), "Choosing radio plan is not automatic success");
                    c.ChooseRadio(c.RadioStep.CorrectOption);
                    Require(c.RadioIndex == 1, "Correct transmission advances stage");
                    c.ChooseRadio((c.RadioStep.CorrectOption + 1) % c.RadioStep.Options.Length);
                    Require(c.RadioMistakes == 1, "Wrong transmission recorded");
                    c.TogglePause();
                    float time = c.RadioRemaining;
                    c.Tick(50);
                    Require(c.RadioRemaining == time, "Pause freezes radio");
                    c.Checkpoint();
                }

                using (var session = new Session(root))
                {
                    var c = session.Campaign;
                    Require(c.Phase == CampaignPhase.Paused, "Paused radio restored");
                    c.TogglePause();
                    Require(c.Phase == CampaignPhase.Radio && c.RadioIndex == 1 && c.RadioMistakes == 1, "Radio progress restored");
                    c.ChooseRadio((c.RadioStep.CorrectOption + 1) % c.RadioStep.Options.Length);
                    Require(c.Phase == CampaignPhase.Running && c.HasFlag("RadioFailed") && !c.HasFlag("RadioGame"), "Two errors fail radio");
                    Require(c.Assign(YuraTask.Courier, out _), "Courier alternative remains available");
                    c.Tick(9);
                    if (c.Phase == CampaignPhase.Dialogue)
                    {
                        c.Choose(0);
                    }

                    Require(c.HasFlag("CourierTracked"), "Fallback produces actionable lead");
                }

                state.Phase = CampaignPhase.Radio;
                state.RadioRemaining = 1;
                File.WriteAllText(Path.Combine(root, "campaign"), JsonUtility.ToJson(state));
                using (var session = new Session(root))
                {
                    session.Campaign.Tick(2);
                    Require(session.Campaign.HasFlag("RadioFailed"), "Radio timeout fails without softlock");
                }
            }
            finally
            {
                Directory.Delete(root, true);
            }
        }
    }
}
