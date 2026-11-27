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
    public static class PrototypeVisualCheck
    {
        private const string Key = "Z57.VisualCheck";
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
                throw new InvalidOperationException("Use isolated batch Editor.");
            }

            string path = Path.Combine(Application.temporaryCachePath, "Z57ValidationDecisions.json");
            foreach (var file in Directory.GetFiles(Path.GetDirectoryName(path), Path.GetFileName(path) + "*"))
            {
                File.Delete(file);
            }

            EditorSceneManager.OpenScene(Zvezda57.Core.SceneNavigation.GamePath);
            SessionState.SetBool(Key, true);
            EditorApplication.EnterPlaymode();
        }

        private static void Changed(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredEditMode && SessionState.GetBool("Z57.VisualFinishing", false))
            {
                SessionState.SetBool("Z57.VisualFinishing", false);
                EditorApplication.delayCall += () => EditorApplication.Exit(0);
                return;
            }

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
                var campaign = scope.Container.Resolve<ICampaignService>();
                var queue = scope.Container.Resolve<ICaseQueue>();
                var review = scope.Container.Resolve<IReviewService>();
                var dispatch = scope.Container.Resolve<IDispatchService>();
                var rig = UnityEngine.Object.FindFirstObjectByType<CameraRig>();
                if (s_stage == 0)
                {
                    Capture("game-entry");
                }

                if (s_stage == 1)
                {
                    Capture("dialogue");
                    campaign.Choose(0);
                    rig.Go(CameraMode.Desk);
                }

                if (s_stage == 2)
                {
                    var folder = GameObject.Find("CaseFolder").GetComponent<WorldAction>();
                    folder.Use();
                    folder.Use();
                    Capture("campaign-desk");
                    rig.Go(CameraMode.Overview);
                }

                if (s_stage == 3)
                {
                    Capture("yura");
                    review.SelectStamp(CaseVerdict.Approve);
                    review.Ink();
                    review.Stamp(0);
                    campaign.SubmitCase();
                    campaign.AnswerCall();
                    campaign.Choose(0);
                    campaign.Tick(4);
                    dispatch.Dispatch(CrewType.Repair, "0.A", out _);
                    campaign.Tick(20);
                    campaign.EndShift();
                }

                if (s_stage == 4)
                {
                    Capture("summary");
                    campaign.Continue();
                    Finish(campaign, queue, review, dispatch);
                }

                if (s_stage == 5)
                {
                    Capture("ending");
                    SessionState.SetBool(Key, false);
                    SessionState.SetBool("Z57.VisualFinishing", true);
                    EditorApplication.update -= Step;
                    EditorApplication.ExitPlaymode();
                    return;
                }

                s_stage++;
                s_due = EditorApplication.timeSinceStartup + 1;
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                SessionState.SetBool(Key, false);
                EditorApplication.Exit(1);
            }
        }

        private static void Finish(ICampaignService campaign, ICaseQueue queue, IReviewService review, IDispatchService dispatch)
        {
            for (int guard = 0; guard < 3000 && campaign.Phase != CampaignPhase.Ending; guard++)
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
                    campaign.Choose(0);
                    continue;
                }

                if (campaign.Phase == CampaignPhase.Summary)
                {
                    campaign.Continue();
                    continue;
                }

                if (queue.State == IntakeState.Waiting || queue.State == IntakeState.Filed)
                {
                    queue.OfferNext();
                }

                if (queue.State == IntakeState.Offered)
                {
                    queue.Receive();
                    review.Reset();
                }

                if (queue.State == IntakeState.Received)
                {
                    if (queue.Current.Id == "3.2")
                    {
                        review.SelectTool(InspectionTool.Magnifier);
                        review.Scan(0, new Vector2(.5f, .75f));
                        review.SelectTool(InspectionTool.Ultraviolet);
                        review.Scan(0, new Vector2(.5f, .75f));
                    }

                    review.SelectStamp(queue.Current.Verdicts.First(x => x.Accepted).Verdict);
                    review.Ink();
                    review.Stamp(0);
                    campaign.SubmitCase();
                }

                foreach (var incident in dispatch.Incidents.Where(x => x.State == IncidentState.Waiting).ToArray())
                {
                    foreach (var crew in incident.AllowedCrews)
                    {
                        if (dispatch.Dispatch(crew, incident.Id, out _))
                        {
                            break;
                        }
                    }
                }

                campaign.Tick(.5f);
                if (campaign.CanEndShift)
                {
                    campaign.EndShift();
                }
            }

            if (campaign.Phase != CampaignPhase.Ending)
            {
                throw new Exception("Visual route did not finish");
            }
        }

        internal static void Capture(string name)
        {
            var camera = UnityEngine.Object.FindFirstObjectByType<CameraRig>().GetComponent<Camera>();
            var canvas = UnityEngine.Object.FindFirstObjectByType<Canvas>();
            var mode = canvas.renderMode;
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = camera.nearClipPlane + .01f;
            var rt = new RenderTexture(1280, 720, 24);
            camera.targetTexture = rt;
            Canvas.ForceUpdateCanvases();
            camera.Render();
            var previous = RenderTexture.active;
            RenderTexture.active = rt;
            var texture = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            texture.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
            texture.Apply();
            Directory.CreateDirectory("/tmp/z57-visual");
            File.WriteAllBytes("/tmp/z57-visual/" + name + ".png", texture.EncodeToPNG());
            RenderTexture.active = previous;
            camera.targetTexture = null;
            canvas.renderMode = mode;
            rt.Release();
            UnityEngine.Object.DestroyImmediate(rt);
            UnityEngine.Object.DestroyImmediate(texture);
        }
    }
}
