using System;
using System.IO;
using System.Linq;
using UnityEditor.SceneManagement;
using UnityEditor;
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
    public static class RemainingVisualCheck
    {
        private const string Key = "Z57.RemainingVisual";
        private static int s_stage;
        private static double s_due;
        [InitializeOnLoadMethod]
        private static void Subscribe()
        {
            EditorApplication.playModeStateChanged += Changed;
        }

        public static void Run()
        {
            if (!Application.isBatchMode)
            {
                throw new InvalidOperationException("Use isolated Editor.");
            }

            SavePaths.Clear();
            EditorSceneManager.OpenScene(Zvezda57.Core.SceneNavigation.GamePath);
            SessionState.SetBool(Key, true);
            EditorApplication.EnterPlaymode();
        }

        private static void Changed(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Key, false))
            {
                return;
            }

            s_stage = 0;
            s_due = EditorApplication.timeSinceStartup + 1;
            EditorApplication.update += Step;
        }

        private static void Step()
        {
            if (EditorApplication.timeSinceStartup < s_due)
            {
                return;
            }

            try
            {
                var scope = UnityEngine.Object.FindFirstObjectByType<OfficeLifetimeScope>();
                var c = scope.Container.Resolve<ICampaignService>();
                var q = scope.Container.Resolve<ICaseQueue>();
                var r = scope.Container.Resolve<IReviewService>();
                var d = scope.Container.Resolve<IDispatchService>();
                if (s_stage == 0)
                {
                    c.Choose(0);
                    UnityEngine.Object.FindFirstObjectByType<CameraRig>().Go(CameraMode.Desk);
                }

                if (s_stage == 1)
                {
                    var folder = GameObject.Find("CaseFolder").GetComponent<WorldAction>();
                    folder.Use();
                    folder.Use();
                    r.SelectStamp(CaseVerdict.Approve);
                    r.Ink();
                    r.Stamp(0);
                    c.Assign(YuraTask.Archive, out _);
                    c.SubmitCase();
                }

                if (s_stage == 2)
                {
                    PrototypeVisualCheck.Capture("incoming-call");
                    c.TogglePause();
                }

                if (s_stage == 3)
                {
                    PrototypeVisualCheck.Capture("pause");
                    c.TogglePause();
                    c.AnswerCall();
                }

                if (s_stage == 4)
                {
                    PrototypeVisualCheck.Capture("answered-call");
                    c.Choose(0);
                    AdvanceToRadio(c, q, r, d);
                }

                if (s_stage == 5)
                {
                    PrototypeVisualCheck.Capture("radio-game");
                    c.ChooseRadio((c.RadioStep.CorrectOption + 1) % c.RadioStep.Options.Length);
                }

                if (s_stage == 6)
                {
                    PrototypeVisualCheck.Capture("radio-error");
                    c.Checkpoint();
                    SessionState.SetBool(Key, false);
                    EditorApplication.update -= Step;
                    EditorApplication.Exit(0);
                    return;
                }

                s_stage++;
                s_due = EditorApplication.timeSinceStartup + .8;
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                SessionState.SetBool(Key, false);
                EditorApplication.Exit(1);
            }
        }

        private static void AdvanceToRadio(ICampaignService c, ICaseQueue q, IReviewService r, IDispatchService d)
        {
            for (int guard = 0; guard < 3000 && c.Phase != CampaignPhase.Radio; guard++)
            {
                if (c.Phase == CampaignPhase.Dialogue)
                {
                    c.Choose(0);
                    continue;
                }

                if (c.CallState == IncomingCallState.Ringing || c.CallState == IncomingCallState.Missed)
                {
                    c.AnswerCall();
                    continue;
                }

                if (c.Phase == CampaignPhase.Summary)
                {
                    c.Continue();
                    continue;
                }

                if (q.State == IntakeState.Waiting || q.State == IntakeState.Filed)
                {
                    q.OfferNext();
                }

                if (q.State == IntakeState.Offered)
                {
                    q.Receive();
                    r.Reset();
                }

                if (q.State == IntakeState.Received)
                {
                    if (q.Current.Id == "3.2")
                    {
                        r.SelectTool(InspectionTool.Magnifier);
                        r.Scan(0, new Vector2(.5f, .75f));
                        r.SelectTool(InspectionTool.Ultraviolet);
                        r.Scan(0, new Vector2(.5f, .75f));
                    }

                    r.SelectStamp(q.Current.Verdicts.First(x => x.Accepted).Verdict);
                    r.Ink();
                    r.Stamp(0);
                    c.SubmitCase();
                }

                foreach (var incident in d.Incidents.Where(x => x.State == IncidentState.Waiting).ToArray())
                {
                    foreach (var crew in incident.AllowedCrews)
                    {
                        if (d.Dispatch(crew, incident.Id, out _))
                        {
                            break;
                        }
                    }
                }

                c.Tick(.5f);
                if (c.CanEndShift)
                {
                    c.EndShift();
                }
            }

            if (c.Phase != CampaignPhase.Radio)
            {
                throw new Exception("Radio route did not start");
            }
        }
    }
}
