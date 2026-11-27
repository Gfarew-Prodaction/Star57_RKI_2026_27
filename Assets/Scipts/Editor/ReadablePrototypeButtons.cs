using System;
using System.Linq;
using TMPro;
using UnityEditor.SceneManagement;
using UnityEditor;
using UnityEngine.UI;
using UnityEngine;
using Zvezda57.Core;

namespace Zvezda57.Editor
{
    public static class ReadablePrototypeButtons
    {
        [MenuItem("Zvezda57/Improve prototype button labels")]
        public static void Apply()
        {
            if (EditorApplication.isPlaying || UnityEngine.SceneManagement.SceneManager.GetActiveScene().isDirty)
            {
                throw new InvalidOperationException("Save the scene and stop Play Mode first.");
            }

            string previous = UnityEngine.SceneManagement.SceneManager.GetActiveScene().path;
            foreach (string path in new[]
            {
                SceneNavigation.GamePath,
                SceneNavigation.MenuPath
            })
            {
                var scene = EditorSceneManager.OpenScene(path);
                var canvas = UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None).First();
                if (PrefabUtility.IsPartOfPrefabInstance(canvas.gameObject))
                {
                    PrefabUtility.UnpackPrefabInstance(canvas.gameObject, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                }

                foreach (var button in canvas.GetComponentsInChildren<Button>(true))
                {
                    var text = button.GetComponentInChildren<TMP_Text>(true);
                    if (text == null)
                    {
                        continue;
                    }

                    text.fontStyle = FontStyles.Bold;
                    text.color = Color.black;
                    text.enableAutoSizing = true;
                    text.fontSize = 30;
                    text.fontSizeMin = 24;
                    text.fontSizeMax = 30;
                    text.alignment = TextAlignmentOptions.Center;
                    var r = text.rectTransform;
                    r.anchorMin = Vector2.zero;
                    r.anchorMax = Vector2.one;
                    r.offsetMin = new Vector2(8, 3);
                    r.offsetMax = new Vector2(-8, -3);
                    var colors = ColorBlock.defaultColorBlock;
                    colors.disabledColor = new Color(.72f, .72f, .72f, 1);
                    button.colors = colors;
                    if (button.targetGraphic != null)
                    {
                        button.targetGraphic.color = Color.white;
                    }
                }

                PrefabUtility.SaveAsPrefabAssetAndConnect(canvas.gameObject, path == SceneNavigation.GamePath ? "Assets/Prefabs/UI/GameInterface.prefab" : "Assets/Prefabs/UI/MainMenu.prefab", InteractionMode.AutomatedAction);
                EditorSceneManager.SaveScene(scene);
            }

            AssetDatabase.SaveAssets();
            if (!string.IsNullOrEmpty(previous))
            {
                EditorSceneManager.OpenScene(previous);
            }

            Debug.Log("Z57_READABLE_BUTTONS_PASS");
        }
    }
}
