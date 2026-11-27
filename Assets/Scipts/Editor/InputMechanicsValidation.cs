using System;
using System.Linq;
using UnityEditor.SceneManagement;
using UnityEditor;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine;
using VContainer;
using Zvezda57.CameraSystem;
using Zvezda57.Core;
using Zvezda57.Documents;
using Zvezda57.Systems;
using UnityInput = UnityEngine.InputSystem.InputSystem;

namespace Zvezda57.Editor
{
    public static class InputMechanicsValidation
    {
        private const string Key = "Z57.InputMechanics";
        private static Mouse s_mouse;
        private static Keyboard s_keyboard;
        private static int s_stage;
        private static double s_due, s_deadline;
        private static bool s_release;
        private static Vector2 s_pointer;
        private static Vector3 s_paperPosition;
        private static string s_paperName;
        private static float s_zoom;
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

            Application.runInBackground = true;
            UnityInput.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            UnityInput.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            foreach (var device in UnityInput.devices.ToArray())
            {
                if (device is Mouse || device is Keyboard)
                {
                    UnityInput.DisableDevice(device);
                }
            }

            s_mouse = UnityInput.AddDevice<Mouse>("ValidationMouse");
            s_keyboard = UnityInput.AddDevice<Keyboard>("ValidationKeyboard");
            s_mouse.MakeCurrent();
            s_keyboard.MakeCurrent();
            s_stage = 0;
            s_release = false;
            s_due = EditorApplication.timeSinceStartup + 1;
            s_deadline = s_due + 60;
            EditorApplication.update += Step;
        }

        private static void Require(bool value, string message)
        {
            if (!value)
            {
                throw new Exception(message);
            }
        }

        private static void Point(Vector2 p, bool pressed = false)
        {
            s_pointer = p;
            UnityInput.QueueStateEvent(s_mouse, new MouseState { position = p }.WithButton(MouseButton.Left, pressed));
            EditorApplication.QueuePlayerLoopUpdate();
        }

        private static void Click(Vector2 p)
        {
            Point(p, true);
            s_release = true;
        }

        private static Vector2 UI(string name)
        {
            var go = GameObject.Find(name);
            Require(go != null, "Missing UI: " + name);
            var rect = go.GetComponent<RectTransform>();
            return RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(rect.rect.center));
        }

        private static Vector2 World(Vector3 point) => UnityEngine.Object.FindFirstObjectByType<CameraRig>().GetComponent<Camera>().WorldToScreenPoint(point);
        private static ICampaignService Campaign() => UnityEngine.Object.FindFirstObjectByType<OfficeLifetimeScope>().Container.Resolve<ICampaignService>();
        private static void Step()
        {
            if (EditorApplication.timeSinceStartup < s_due)
            {
                return;
            }

            try
            {
                Require(EditorApplication.timeSinceStartup < s_deadline, "Input test timed out at stage " + s_stage);
                if (s_release)
                {
                    Point(s_pointer);
                    s_release = false;
                    s_due = EditorApplication.timeSinceStartup + .7;
                    return;
                }

                if (s_stage == 0)
                {
                    Click(UI("Dialogue choice 1"));
                    s_stage++;
                }
                else if (s_stage == 1)
                {
                    Require(Campaign().Phase == CampaignPhase.Running, "Input System clicks dialogue choice");
                    UnityEngine.Object.FindFirstObjectByType<CameraRig>().Go(CameraMode.Desk);
                    s_stage++;
                }
                else if (s_stage == 2)
                {
                    Click(World(GameObject.Find("CaseFolder").transform.position));
                    s_stage++;
                }
                else if (s_stage == 3)
                {
                    var queue = UnityEngine.Object.FindFirstObjectByType<OfficeLifetimeScope>().Container.Resolve<ICaseQueue>();
                    Require(queue.State == IntakeState.Received, "Physical folder receives mouse click");
                    Click(World(GameObject.Find("CaseFolder").transform.position));
                    s_stage++;
                }
                else if (s_stage == 4)
                {
                    var paper = UnityEngine.Object.FindObjectsByType<DraggableDocument>(FindObjectsSortMode.None).First();
                    s_paperName = paper.name;
                    s_paperPosition = paper.transform.position;
                    Point(World(s_paperPosition), true);
                    s_stage++;
                }
                else if (s_stage == 5)
                {
                    s_paperPosition += new Vector3(.15f, 0, .15f);
                    Point(World(s_paperPosition), true);
                    s_stage++;
                }
                else if (s_stage == 6)
                {
                    Point(s_pointer);
                    s_stage++;
                }
                else if (s_stage == 7)
                {
                    var paper = GameObject.Find(s_paperName).GetComponent<DraggableDocument>();
                    Require(Vector3.Distance(paper.SavedPosition, s_paperPosition) < .02f, "Input System drags and drops paper");
                    s_paperPosition = paper.SavedPosition;
                    Click(World(s_paperPosition));
                    s_stage++;
                }
                else if (s_stage == 8)
                {
                    Require(UnityEngine.Object.FindFirstObjectByType<CameraRig>().Mode == CameraMode.Inspect, "Click opens document");
                    UnityInput.QueueStateEvent(s_mouse, new MouseState { position = s_pointer, scroll = new Vector2(0, 120) });
                    s_stage++;
                }
                else if (s_stage == 9)
                {
                    s_zoom = UnityEngine.Object.FindFirstObjectByType<Zvezda57.UI.DeskHud>().Zoom;
                    Require(s_zoom > 26, "Scroll zooms document");
                    Click(UI("Pause"));
                    s_stage++;
                }
                else if (s_stage == 10)
                {
                    Require(Campaign().Phase == CampaignPhase.Paused, "Mouse opens pause");
                    UnityInput.QueueStateEvent(s_keyboard, new KeyboardState(UnityEngine.InputSystem.Key.Escape));
                    s_stage++;
                }
                else if (s_stage == 11)
                {
                    UnityInput.QueueStateEvent(s_keyboard, new KeyboardState());
                    Require(Campaign().Phase == CampaignPhase.Running, "Escape resumes game");
                    Click(UI("Actions"));
                    s_stage++;
                }
                else if (s_stage == 12)
                {
                    Require(GameObject.Find("Action drawer") != null, "Actions drawer opens through Input System");
                    Click(UI("Main menu"));
                    s_stage++;
                }
                else if (s_stage == 13)
                {
                    if (SceneManager.GetActiveScene().name != "Menu" || SceneNavigation.IsLoading)
                    {
                        return;
                    }

                    Click(UI("Continue"));
                    s_stage++;
                }
                else if (s_stage == 14)
                {
                    if (SceneManager.GetActiveScene().name != "MainGame" || SceneNavigation.IsLoading)
                    {
                        return;
                    }

                    Require(Campaign().Phase == CampaignPhase.Running, "Continue restores running phase");
                    Require(UnityEngine.Object.FindFirstObjectByType<CameraRig>().Mode == CameraMode.Inspect, "Reading camera restored");
                    var paper = UnityEngine.Object.FindObjectsByType<DraggableDocument>(FindObjectsSortMode.None).Single(x => x.name == s_paperName);
                    Require(Vector3.Distance(paper.SavedPosition, s_paperPosition) < .02f, "Actual moved paper restored after scene load");
                    Require(Math.Abs(UnityEngine.Object.FindFirstObjectByType<Zvezda57.UI.DeskHud>().Zoom - s_zoom) < .01f, "Document zoom restored");
                    Debug.Log("Z57_INPUT_MECHANICS_PASS: virtual mouse/keyboard events click dialogue and folder, drag paper, inspect, scroll, pause/resume, menu/continue and actual view restoration.");
                    Finish(0);
                    return;
                }

                s_due = EditorApplication.timeSinceStartup + .7;
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                Finish(1);
            }
        }

        private static void Finish(int result)
        {
            SessionState.SetBool(Key, false);
            EditorApplication.update -= Step;
            EditorApplication.Exit(result);
        }
    }
}
