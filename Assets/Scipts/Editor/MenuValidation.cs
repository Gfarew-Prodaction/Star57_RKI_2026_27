using System;
using UnityEditor.SceneManagement;
using UnityEditor;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine;
using VContainer;
using Zvezda57.Core;
using Zvezda57.Systems;

namespace Zvezda57.Editor
{
    public static class MenuValidation
    {
        private const string Key = "Z57.MenuValidation";
        private static int s_step;
        private static double s_due, s_deadline;
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

            SavePaths.Clear();
            EditorSceneManager.OpenScene(SceneNavigation.MenuPath);
            SessionState.SetBool(Key, true);
            EditorApplication.EnterPlaymode();
        }

        private static void Changed(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(Key, false))
            {
                s_step = 0;
                s_due = EditorApplication.timeSinceStartup + 1;
                s_deadline = s_due + 40;
                EditorApplication.update += Check;
            }
        }

        private static void Require(bool value, string text)
        {
            if (!value)
            {
                throw new Exception(text);
            }
        }

        private static Button Button(string name)
        {
            var go = GameObject.Find(name);
            Require(go != null, "Missing button: " + name);
            return go.GetComponent<Button>();
        }

        private static void ReturnToMenu()
        {
            if (GameObject.Find("Main menu") == null)
            {
                Button("Actions").onClick.Invoke();
            }

            Button("Main menu").onClick.Invoke();
        }

        private static ICampaignService Campaign() => UnityEngine.Object.FindFirstObjectByType<OfficeLifetimeScope>().Container.Resolve<ICampaignService>();
        private static bool In(string scene) => SceneManager.GetActiveScene().name == scene && !SceneNavigation.IsLoading;
        private static void Check()
        {
            if (EditorApplication.timeSinceStartup < s_due)
            {
                return;
            }

            try
            {
                Require(EditorApplication.timeSinceStartup < s_deadline, "Menu navigation timeout");
                if (s_step == 0)
                {
                    Require(In("Menu") && !Button("Continue").interactable, "No save: continue disabled");
                    CaptureMenu();
                    Button("New game").onClick.Invoke();
                    s_step++;
                }
                else if (s_step == 1 && In("MainGame"))
                {
                    Require(Campaign().Phase == CampaignPhase.Dialogue, "Game starts intro automatically");
                    Campaign().Choose(0);
                    Require(Campaign().YuraTrust == 1, "Choice saved");
                    s_step++;
                }
                else if (s_step == 2 && In("MainGame"))
                {
                    ReturnToMenu();
                    s_step++;
                }
                else if (s_step == 3 && In("Menu"))
                {
                    Require(Button("Continue").interactable, "Save enables continue");
                    Button("New game").onClick.Invoke();
                    Require(GameObject.Find("Confirm new game") != null, "New game requires reset confirmation");
                    Button("Cancel").onClick.Invoke();
                    Require(SavePaths.HasProgress, "Cancel preserves progress");
                    Button("Continue").onClick.Invoke();
                    s_step++;
                }
                else if (s_step == 4 && In("MainGame"))
                {
                    Require(Campaign().Phase == CampaignPhase.Running && Campaign().YuraTrust == 1, "Continue restores state without repeating intro");
                    ReturnToMenu();
                    s_step++;
                }
                else if (s_step == 5 && In("Menu"))
                {
                    Button("New game").onClick.Invoke();
                    Button("Start new game").onClick.Invoke();
                    s_step++;
                }
                else if (s_step == 6 && In("MainGame"))
                {
                    Require(Campaign().Phase == CampaignPhase.Dialogue && Campaign().YuraTrust == 0, "Confirmed reset starts fresh campaign");
                    Debug.Log("Z57_MENU_PASS: separate first scene, disabled continue, game autostart, return to menu, cancel reset, resume and confirmed new game.");
                    Finish(0);
                    return;
                }

                s_due = EditorApplication.timeSinceStartup + .5;
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                Finish(1);
            }
        }

        private static void CaptureMenu()
        {
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null)
            {
                return;
            }

            var camera = Camera.main;
            var canvas = UnityEngine.Object.FindFirstObjectByType<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = camera.nearClipPlane + .01f;
            var rt = new RenderTexture(1280, 720, 24);
            camera.targetTexture = rt;
            Canvas.ForceUpdateCanvases();
            camera.Render();
            var previous = RenderTexture.active;
            RenderTexture.active = rt;
            var image = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
            image.Apply();
            System.IO.Directory.CreateDirectory("/tmp/z57-visual");
            System.IO.File.WriteAllBytes("/tmp/z57-visual/main-menu.png", image.EncodeToPNG());
            RenderTexture.active = previous;
            camera.targetTexture = null;
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            rt.Release();
            UnityEngine.Object.DestroyImmediate(rt);
            UnityEngine.Object.DestroyImmediate(image);
        }

        private static void Finish(int code)
        {
            SavePaths.Clear();
            SessionState.SetBool(Key, false);
            EditorApplication.update -= Check;
            EditorApplication.Exit(code);
        }
    }
}
