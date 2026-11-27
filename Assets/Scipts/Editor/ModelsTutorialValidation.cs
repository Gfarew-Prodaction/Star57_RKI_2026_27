using System;
using System.Linq;
using System.Reflection;
using UnityEditor.SceneManagement;
using UnityEditor;
using UnityEngine;
using VContainer;
using Zvezda57.CameraSystem;
using Zvezda57.Core;
using Zvezda57.Dispatch;
using Zvezda57.Documents;
using Zvezda57.Systems;
using Zvezda57.UI;

namespace Zvezda57.Editor
{
    public static class ModelsTutorialValidation
    {
        private const string Key = "Z57.ModelsTutorial";
        private static int s_step;
        private static double s_due;
        [InitializeOnLoadMethod]
        private static void Subscribe() => EditorApplication.playModeStateChanged += Changed;
        public static void AuthorAndRun()
        {
            ModelIntegration.Apply();
            Run();
        }

        public static void Run()
        {
            if (!Application.isBatchMode)
            {
                throw new InvalidOperationException("Use the isolated validation project.");
            }

            SavePaths.Clear();
            EditorSceneManager.OpenScene(SceneNavigation.GamePath);
            SessionState.SetBool(Key, true);
            EditorApplication.EnterPlaymode();
        }

        private static void Changed(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Key, false))
            {
                return;
            }

            s_step = 0;
            s_due = EditorApplication.timeSinceStartup + 1;
            EditorApplication.update += Tick;
        }

        private static void Require(bool ok, string message)
        {
            if (!ok)
            {
                throw new Exception(message);
            }
        }

        private static void Tick()
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
                var rig = UnityEngine.Object.FindFirstObjectByType<CameraRig>();
                var guide = UnityEngine.Object.FindFirstObjectByType<TutorialGuide>();
                void Has(string text) => Require(guide.CurrentInstruction.Contains(text), "Tutorial step " + s_step + ": " + guide.CurrentInstruction);
                if (s_step == 0)
                {
                    Require(guide != null, "Authored tutorial exists");
                    Require(UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None).Count(x => x.name.EndsWith(" model")) == 6, "All six authored models exist");
                    c.Choose(0);
                    rig.Go(CameraMode.Desk);
                }

                if (s_step == 1)
                {
                    Has("1/7");
                    if (UnityEngine.Object.FindFirstObjectByType<CompactHud>() != null)
                    {
                        Require(GameObject.Find("Action drawer") == null, "Action drawer starts closed");
                        Require(GameObject.Find("File case") == null, "Submission stays hidden before stamping");
                        var button = UnityEngine.Object.FindObjectsByType<UnityEngine.UI.Button>(FindObjectsSortMode.None).Single(x => x.name == "Tutorial help");
                        button.onClick.Invoke();
                        Require(GameObject.Find("Tutorial details") != null, "Optional explanation opens");
                        PrototypeVisualCheck.Capture("compact-tutorial-help");
                        button.onClick.Invoke();
                        Require(GameObject.Find("Tutorial details") == null, "Explanation collapses");
                    }

                    PrototypeVisualCheck.Capture("models-tutorial-folder");
                    q.Receive();
                }

                if (s_step == 2)
                {
                    Has("2/7");
                    typeof(OfficePrototype).GetMethod("FolderClick", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(UnityEngine.Object.FindFirstObjectByType<OfficePrototype>(), null);
                }

                if (s_step == 3)
                {
                    Has("3/7");
                    UnityEngine.Object.FindFirstObjectByType<DraggableDocument>().Use();
                }

                if (s_step == 4)
                {
                    Has("3/7");
                    PrototypeVisualCheck.Capture("compact-document-reader");
                    rig.Go(CameraMode.Desk);
                }

                if (s_step == 5)
                {
                    Has("4/7");
                    PrototypeVisualCheck.Capture("models-tutorial-stamp");
                    r.SelectStamp(CaseVerdict.Approve);
                }

                if (s_step == 6)
                {
                    Has("5/7");
                    r.Ink();
                }

                if (s_step == 7)
                {
                    Has("6/7");
                    r.Stamp(0);
                }

                if (s_step == 8)
                {
                    Has("7/7");
                    if (UnityEngine.Object.FindFirstObjectByType<CompactHud>() != null)
                    {
                        Require(GameObject.Find("File case") != null, "Submission appears after stamping");
                    }

                    PrototypeVisualCheck.Capture("models-tutorial-submit");
                    c.SubmitCase();
                    c.AnswerCall();
                    c.Choose(0);
                    rig.Go(CameraMode.Overview);
                }

                if (s_step == 9)
                {
                    PrototypeVisualCheck.Capture("models-overview");
                    Debug.Log("Z57_MODELS_TUTORIAL_PASS: six scene models and all seven reactive tutorial steps.");
                    SessionState.SetBool(Key, false);
                    EditorApplication.update -= Tick;
                    EditorApplication.Exit(0);
                    return;
                }

                s_step++;
                s_due = EditorApplication.timeSinceStartup + 1;
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                SessionState.SetBool(Key, false);
                EditorApplication.update -= Tick;
                EditorApplication.Exit(1);
            }
        }
    }
}
