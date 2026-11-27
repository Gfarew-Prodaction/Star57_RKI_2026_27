using System;
using System.IO;
using TMPro;
using UnityEditor.SceneManagement;
using UnityEditor;
using UnityEngine.UI;
using UnityEngine;
using Zvezda57.Core;
using Zvezda57.UI;

namespace Zvezda57.Editor
{
    public static class AuthoredLayoutValidation
    {
        private const string Key = "Z57.AuthoredLayoutTest";

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

            File.Copy(SceneNavigation.MenuPath, "/tmp/z57-menu-authored-backup.unity", true);
            EditorSceneManager.OpenScene(SceneNavigation.MenuPath);
            var button = GameObject.Find("New game");
            button.name = "Edited new game button";
            PrefabUtility.RecordPrefabInstancePropertyModifications(button);
            var rect = button.GetComponent<RectTransform>();
            rect.anchoredPosition = new Vector2(720, -710);
            var image = button.GetComponent<Image>();
            image.color = new Color(.6f, .15f, .5f);
            var text = button.GetComponentInChildren<TMP_Text>();
            text.enableAutoSizing = false;
            text.fontSize = 31;
            text.text = "Авторская подпись";
            text.GetComponent<AuthoredText>().OverrideContent = true;
            foreach (var component in new Component[]
            {
                rect,
                image,
                text,
                text.GetComponent<AuthoredText>()
            })
            {
                EditorUtility.SetDirty(component);
                PrefabUtility.RecordPrefabInstancePropertyModifications(component);
            }

            EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            SessionState.SetBool(Key, true);
            EditorApplication.EnterPlaymode();
        }

        private static void Changed(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(Key, false))
            {
                EditorApplication.update += Check;
            }
        }

        private static void Check()
        {
            EditorApplication.update -= Check;
            int result = 0;
            try
            {
                var button = GameObject.Find("Edited new game button");
                var text = button.GetComponentInChildren<TMP_Text>();
                Vector2 position = button.GetComponent<RectTransform>().anchoredPosition;
                Color color = button.GetComponent<Image>().color;
                if (position != new Vector2(720, -710) || Mathf.Abs(color.r - .6f) > .001f || text.fontSize != 31 || text.text != "Авторская подпись")
                {
                    throw new Exception($"Inspector edits differ in play mode: {position}, {color}, {text.fontSize}, {text.text}");
                }

                button.GetComponent<Button>().onClick.Invoke();
                if (!SceneNavigation.IsLoading && GameObject.Find("Confirm new game") == null)
                {
                    throw new Exception("Renamed button no longer starts a new game");
                }

                Debug.Log("Z57_AUTHORED_LAYOUT_PASS: edited position, color, TMP size and caption persist; renamed button still starts a new game.");
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                result = 1;
            }

            File.Copy("/tmp/z57-menu-authored-backup.unity", SceneNavigation.MenuPath, true);
            File.Delete("/tmp/z57-menu-authored-backup.unity");
            SessionState.SetBool(Key, false);
            EditorApplication.Exit(result);
        }
    }
}
