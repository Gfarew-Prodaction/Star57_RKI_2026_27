using System;
using System.IO;
using UnityEditor.SceneManagement;
using UnityEditor;
using UnityEngine.SceneManagement;
using Zvezda57.Core;

namespace Zvezda57.Editor
{
    public static class IsolatedPlaySession
    {
        [InitializeOnLoadMethod]
        private static void Subscribe()
        {
            EditorApplication.playModeStateChanged += Changed;
        }

        [MenuItem("Zvezda57/Play mechanics with isolated saves")]
        public static void Start()
        {
            if (SceneManager.GetActiveScene().isDirty)
            {
                throw new InvalidOperationException("Save the current scene before starting an isolated play session.");
            }

            string folder = Path.Combine(Path.GetTempPath(), "Z57Manual-" + Guid.NewGuid());
            Directory.CreateDirectory(folder);
            SessionState.SetString("Z57.ManualPreviousScene", SceneManager.GetActiveScene().path);
            SessionState.SetString("Z57.ManualSavePath", Path.Combine(folder, "decisions.json"));
            EditorSceneManager.OpenScene(SceneNavigation.MenuPath);
            EditorApplication.EnterPlaymode();
        }

        [MenuItem("Zvezda57/Play mechanics with isolated saves", true)]
        private static bool Available() => !EditorApplication.isPlayingOrWillChangePlaymode;
        private static void Changed(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredEditMode || string.IsNullOrEmpty(SessionState.GetString("Z57.ManualSavePath", "")))
            {
                return;
            }

            string previous = SessionState.GetString("Z57.ManualPreviousScene", "");
            SessionState.EraseString("Z57.ManualSavePath");
            SessionState.EraseString("Z57.ManualPreviousScene");
            if (!string.IsNullOrEmpty(previous) && File.Exists(previous))
            {
                EditorApplication.delayCall += () => EditorSceneManager.OpenScene(previous);
            }
        }
    }
}
