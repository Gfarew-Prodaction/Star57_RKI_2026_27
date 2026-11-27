using System;
using System.IO;
using System.Linq;
using UnityEditor.SceneManagement;
using UnityEditor;
using UnityEngine.UI;
using UnityEngine;
using VContainer;
using Zvezda57.CameraSystem;
using Zvezda57.Core;
using Zvezda57.Dispatch;
using Zvezda57.Documents;
using Zvezda57.InputSystem;
using Zvezda57.Systems;

namespace Zvezda57.Editor
{
    public static class CampaignValidation
    {
        private const string Key = "Z57.CampaignValidation";
        private static int s_step;
        private static double s_deadline;

        private static void Require(bool condition, string text)
        {
            if (!condition)
            {
                throw new Exception(text);
            }
        }

        [InitializeOnLoadMethod]
        private static void Subscribe()
        {
            EditorApplication.playModeStateChanged += Changed;
        }

        public static void Run()
        {
            if (!Application.isBatchMode)
            {
                throw new InvalidOperationException("Use isolated batch Editor.");
            }

            foreach (var scenario in new[]
            {
                EndingKind.Good,
                EndingKind.Mixed,
                EndingKind.Bad
            })
            {
                Simulate(scenario);
            }

            Require(CampaignService.Evaluate(true, true, 100, 0, 100, 60) == EndingKind.Bad, "Critical metric has priority over successful intercept");
            string root = Path.Combine(Application.temporaryCachePath, "Z57ValidationDecisions.json");
            foreach (var file in Directory.GetFiles(Path.GetDirectoryName(root), Path.GetFileName(root) + "*"))
            {
                File.Delete(file);
            }

            EditorSceneManager.OpenScene(Zvezda57.Core.SceneNavigation.GamePath);
            SessionState.SetBool(Key, true);
            EditorApplication.EnterPlaymode();
        }

        private static T[] Assets<T>(string folder)
            where T : UnityEngine.Object => AssetDatabase.FindAssets("t:" + typeof(T).Name, new[] { folder }).Select(g => AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(g))).ToArray();
        private static void Simulate(EndingKind expected)
        {
            var content = AssetDatabase.LoadAssetAtPath<CampaignDefinition>("Assets/Settings/Shifts/Campaign.asset");
            var cases = Assets<CaseDefinition>("Assets/Settings/Cases");
            var incidents = Assets<IncidentDefinition>("Assets/Settings/Incidents");
            var upgradesData = Assets<UpgradeDefinition>("Assets/Settings/Upgrades");
            string root = Path.Combine(Path.GetTempPath(), "Z57Campaign-" + Guid.NewGuid());
            Directory.CreateDirectory(root);
            var journal = new JsonDecisionStore(Path.Combine(root, "decisions.json"));
            var queue = new CaseQueue(cases);
            var dispatch = new DispatchService(incidents);
            var metrics = new CityMetrics();
            var upgrades = new UpgradeService(upgradesData, dispatch, Path.Combine(root, "upgrades.json"));
            var review = new ReviewService(queue, journal);
            using (var campaign = new CampaignService(content, cases, queue, journal, dispatch, metrics, upgrades, Path.Combine(root, "campaign.json"), review))
            {
                campaign.Start();
                Require(campaign.Phase == CampaignPhase.Dialogue, "Campaign begins with dialogue");
                Require(!campaign.EndShift(), "Cannot skip tutorial");
                for (int day = 0; day < 4; day++)
                {
                    Require(campaign.ShiftIndex == day, "Ordered shift progression");
                    int steps = 0;
                    while (!campaign.CanEndShift && steps++ < 1500)
                    {
                        if (campaign.CallState == IncomingCallState.Ringing || campaign.CallState == IncomingCallState.Missed)
                        {
                            campaign.AnswerCall();
                            continue;
                        }

                        if (campaign.Phase == CampaignPhase.Radio)
                        {
                            campaign.ChooseRadio(campaign.RadioStep.CorrectOption);
                            continue;
                        }

                        if (campaign.Phase == CampaignPhase.Dialogue)
                        {
                            var dialogue = campaign.Dialogue;
                            float taskBefore = campaign.TaskRemaining;
                            float before = dispatch.Incidents.Sum(x => x.Remaining);
                            campaign.Tick(10);
                            Require(Math.Abs(taskBefore - campaign.TaskRemaining) < .001f, "Dialogue pauses Yura assignment");
                            Require(Math.Abs(dispatch.Incidents.Sum(x => x.Remaining) - before) < .001f, "Blocking dialogue pauses clocks");
                            int choice = dialogue.Id == "D3_YURA" && expected == EndingKind.Mixed ? 1 : 0;
                            Require(campaign.Choose(choice), "Dialogue choice accepted");
                            continue;
                        }

                        if (day == 3 && expected == EndingKind.Mixed && !campaign.HasFlag("CourierTracked") && campaign.Task == YuraTask.None)
                        {
                            campaign.Assign(YuraTask.Courier, out _);
                        }

                        if (queue.State == IntakeState.Waiting || queue.State == IntakeState.Filed)
                        {
                            queue.OfferNext();
                        }

                        if (queue.State == IntakeState.Offered)
                        {
                            Require(content.Shifts[day].CaseIds.Contains(queue.Current.Id), "No future case leaks into shift");
                            queue.Receive();
                            review.Reset();
                        }

                        if (queue.State == IntakeState.Received)
                        {
                            string id = queue.Current.Id;
                            if (id == "3.2")
                            {
                                review.SelectTool(InspectionTool.Magnifier);
                                review.Scan(0, new Vector2(.5f, .75f));
                                review.SelectTool(InspectionTool.Ultraviolet);
                                review.Scan(0, new Vector2(.5f, .75f));
                            }

                            CaseVerdict verdict = queue.Current.Verdicts.First(x => x.Accepted).Verdict;
                            if (expected == EndingKind.Mixed && (id == "3.1" || id == "2.2"))
                            {
                                verdict = CaseVerdict.Detain;
                            }

                            review.SelectStamp(verdict);
                            review.Ink();
                            review.Stamp(0);
                            campaign.SubmitCase();
                        }

                        foreach (var incident in dispatch.Incidents.Where(x => x.State == IncidentState.Waiting).ToArray())
                        {
                            if (expected == EndingKind.Bad && incident.Id == "3.C")
                            {
                                continue;
                            }

                            if (expected == EndingKind.Mixed && incident.Id == "1.A")
                            {
                                continue;
                            }

                            foreach (var crew in incident.AllowedCrews)
                            {
                                if (dispatch.Dispatch(crew, incident.Id, out _))
                                {
                                    break;
                                }
                            }
                        }

                        campaign.Tick(.5f);
                    }

                    Require(steps < 1500, "Shift must terminate without a dead end: " + day + "/" + expected);
                    Require(campaign.EndShift() && campaign.Phase == CampaignPhase.Summary, "Explicit summary transition");
                    if (day == 1)
                    {
                        Require(upgrades.Points == 3, "Reward after day 1");
                    }

                    if (day == 2)
                    {
                        Require(upgrades.Points == 5, "Total reserve after day 2");
                    }

                    Require(campaign.Continue(), "Continue from summary");
                }

                Require(campaign.Phase == CampaignPhase.Ending && campaign.Ending.Kind == expected, "Playable outcome: " + expected + " actual " + campaign.Ending.Kind + " reactor " + metrics.Reactor);
                Require(journal.Records.Count == 7, "Seven cases completed across four shifts");
                var secondQueue = new CaseQueue(cases);
                var secondDispatch = new DispatchService(incidents);
                var restoredMetrics = new CityMetrics();
                using (var restored = new CampaignService(content, cases, secondQueue, journal, secondDispatch, restoredMetrics, upgrades, Path.Combine(root, "campaign.json"), new ReviewService(secondQueue, journal)))
                {
                    restored.Start();
                    Require(restored.Phase == CampaignPhase.Ending && restored.Ending.Kind == expected, "Ending restored without replaying campaign");
                    Require(restoredMetrics.Reactor == metrics.Reactor && restoredMetrics.Secrecy == metrics.Secrecy && restoredMetrics.Calm == metrics.Calm, "Exact metric snapshot restored");
                }

                Debug.Log("Z57_CAMPAIGN_ROUTE_PASS: " + expected + "; four shifts, seven cases, dialogue pause, services, rewards, save/reload.");
            }

            Directory.Delete(root, true);
        }

        private static void Changed(PlayModeStateChange change)
        {
            if (change == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(Key, false))
            {
                s_step = 0;
                s_deadline = EditorApplication.timeSinceStartup + 20;
                EditorApplication.update += CheckScene;
            }
        }

        private static void Click(string name)
        {
            var control = UnityEngine.Object.FindObjectsByType<Button>(FindObjectsSortMode.None)
                .SingleOrDefault(button => button.name == name && button.isActiveAndEnabled);
            Require(control != null, "Missing control: " + name);
            control.onClick.Invoke();
        }

        private static void CheckScene()
        {
            try
            {
                Require(EditorApplication.timeSinceStartup < s_deadline, $"Campaign UI timed out at step {s_step}");
                var scope = UnityEngine.Object.FindFirstObjectByType<OfficeLifetimeScope>();
                var campaign = scope.Container.Resolve<ICampaignService>();
                switch (s_step)
                {
                    case 0:
                        CheckDeskAndStamp(scope, campaign);
                        break;
                    case 1:
                        if (GameObject.Find("File case") == null)
                        {
                            return;
                        }

                        Click("File case");
                        Click("Confirm verdict");
                        Require(campaign.CallState == IncomingCallState.Ringing, "Incoming phone call after tutorial");
                        Click("Answer call");
                        Require(campaign.Dialogue.Id == "D0_PHONE", "Answered call opens dialogue");
                        Click("Dialogue choice 1");
                        campaign.Tick(4);
                        scope.Container.Resolve<IDispatchService>().Dispatch(CrewType.Repair, "0.A", out _);
                        campaign.Tick(20);
                        Require(campaign.CanEndShift, "Tutorial reaches summary readiness");
                        break;
                    case 2:
                        if (GameObject.Find("End shift") == null)
                        {
                            return;
                        }

                        CheckSummary(campaign);
                        Debug.Log("Z57_CAMPAIGN_PLAY_PASS: intro choice, physical tutorial folder/stamp/pad, phone dial, reference tabs, incoming call, summary, upgrades, next-shift UI and 3D Yura.");
                        Finish(0);
                        return;
                }

                s_step++;
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                Finish(1);
            }
        }

        private static void CheckDeskAndStamp(OfficeLifetimeScope scope, ICampaignService campaign)
        {
            var queue = scope.Container.Resolve<ICaseQueue>();
            var rig = UnityEngine.Object.FindFirstObjectByType<CameraRig>();
            var investigation = scope.GetComponent<Zvezda57.UI.InvestigationHud>();
            Require(campaign.Phase == CampaignPhase.Dialogue, "Intro modal appears");
            Click("Dialogue choice 1");
            Require(queue.Current.Id == "0.1" && queue.Current.DeliveredByYura, "Tutorial delivered by Yura without visitor portrait");
            rig.Go(CameraMode.Phone);
            Click("Digit 2");
            Click("Digit 0");
            Click("Digit 4");
            Require(investigation.PhoneDigits == "204", "Phone buttons update the dial");
            Click("Dial");
            Require(!string.IsNullOrEmpty(investigation.PhoneAnswer), "Phone displays an answer");
            Click("Hang up");
            Require(GameObject.Find("Phone dial") == null, "Hang up closes the phone panel");
            rig.Go(CameraMode.Desk);
            var reference = GameObject.Find("ReferenceBook").GetComponent<WorldAction>();
            reference.Use();
            Click("Reference tab 2");
            Require(investigation.ReferenceOpen && investigation.ReferenceTab == 1, "Reference tab switches");
            reference.Use();
            var folder = GameObject.Find("CaseFolder").GetComponent<WorldAction>();
            folder.Use();
            folder.Use();
            Require(queue.State == IntakeState.Received, "World tutorial folder opens");
            GameObject.Find("Stamp Approve").GetComponent<WorldAction>().Use();
            GameObject.Find("Ink pad").GetComponent<WorldAction>().Use();
            UnityEngine.Object.FindFirstObjectByType<DraggableDocument>().Use();
            Require(scope.Container.Resolve<IReviewService>().CanSubmit, "Tutorial document is stamped");
        }

        private static void CheckSummary(ICampaignService campaign)
        {
            Click("End shift");
            Require(campaign.Phase == CampaignPhase.Summary, "Summary UI");
            Click("Campaign action");
            var upgrades = GameObject.Find("Reserves and upgrades");
            Require(upgrades != null, "Summary opens upgrades");
            var close = upgrades.GetComponentsInChildren<Button>().Single(button => button.name == "Close");
            Click("Clear selection");
            close.onClick.Invoke();
            Require(!upgrades.activeSelf, "Upgrades close independently of the summary");
            Click("Campaign proceed");
            Require(campaign.ShiftIndex == 1 && campaign.Phase == CampaignPhase.Dialogue, "Next shift intro UI");
            Require(UnityEngine.Object.FindFirstObjectByType<Zvezda57.Visitors.YuraPresenter>() != null, "3D Yura presenter exists");
        }

        private static void Finish(int result)
        {
            EditorApplication.update -= CheckScene;
            SessionState.SetBool(Key, false);
            EditorApplication.Exit(result);
        }
    }
}
