using System;
using System.Linq;
using TMPro;
using UnityEditor.SceneManagement;
using UnityEditor;
using UnityEngine.UI;
using UnityEngine;
using Zvezda57.Core;
using Zvezda57.UI;

namespace Zvezda57.Editor
{
    public static class CompactHudAuthoring
    {
        [MenuItem("Zvezda57/Apply compact gameplay HUD")]
        public static void Apply()
        {
            if (EditorApplication.isPlaying || UnityEngine.SceneManagement.SceneManager.GetActiveScene().isDirty)
            {
                throw new InvalidOperationException("Save the scene and stop Play Mode first.");
            }

            string previous = UnityEngine.SceneManagement.SceneManager.GetActiveScene().path;
            var scene = EditorSceneManager.OpenScene(SceneNavigation.GamePath);
            Transform Find(string name) => UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None).First(x => x.name == name);
            var canvas = Find("CanvasHUD");
            if (PrefabUtility.IsPartOfPrefabInstance(canvas.gameObject))
            {
                PrefabUtility.UnpackPrefabInstance(canvas.gameObject, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            }

            var desk = new SerializedObject(UnityEngine.Object.FindAnyObjectByType<DeskHud>());
            var campaign = new SerializedObject(UnityEngine.Object.FindAnyObjectByType<CampaignHud>());
            var operations = new SerializedObject(UnityEngine.Object.FindAnyObjectByType<OperationsHud>());
            var investigation = new SerializedObject(UnityEngine.Object.FindAnyObjectByType<InvestigationHud>());
            Button Button(SerializedObject owner, string field) => (Button)owner.FindProperty(field).objectReferenceValue;
            GameObject LabelObject(SerializedObject owner, string field) => ((TMP_Text)owner.FindProperty(field).objectReferenceValue).gameObject;
            foreach (var child in canvas.GetComponentsInChildren<Transform>(true).Where(x => x.name.StartsWith("Prototype ")))
            {
                child.gameObject.SetActive(false);
            }

            var header = Panel(canvas, "Compact header", new Vector2(1920, 82));
            Stretch(header.GetComponent<RectTransform>(), true, 82);
            var footer = Panel(canvas, "Compact footer", new Vector2(1920, 80));
            Stretch(footer.GetComponent<RectTransform>(), false, 80);
            var drawer = Panel(canvas, "Action drawer", new Vector2(340, 405));
            Bottom(drawer.GetComponent<RectTransform>(), new Vector2(400, 95), new Vector2(340, 405));
            var shift = LabelObject(campaign, "status");
            Place(shift, header.transform, new Vector2(20, -7), new Vector2(850, 32));
            Text(shift, 22);
            var city = LabelObject(operations, "values");
            Place(city, header.transform, new Vector2(0, -7), new Vector2(760, 32));
            Right(city.GetComponent<RectTransform>(), new Vector2(-20, -7));
            Text(city, 21);
            var status = LabelObject(desk, "status");
            Place(status, header.transform, new Vector2(20, -45), new Vector2(400, 30));
            Text(status, 20);
            var tool = LabelObject(investigation, "tool");
            Place(tool, header.transform, new Vector2(440, -45), new Vector2(650, 30));
            Text(tool, 20);
            var alert = LabelObject(investigation, "alert");
            Place(alert, header.transform, Vector2.zero, new Vector2(350, 30));
            Right(alert.GetComponent<RectTransform>(), new Vector2(-20, -45));
            Text(alert, 20);
            var back = Button(desk, "backButton");
            BottomButton(back, footer.transform, 20, 130);
            var next = Button(desk, "nextVisitorButton");
            BottomButton(next, footer.transform, 165, 220);
            var submit = Button(desk, "fileButton");
            BottomButton(submit, footer.transform, 165, 220);
            var end = Button(campaign, "endShiftButton");
            BottomButton(end, footer.transform, 165, 220);
            var more = NewButton(footer.transform, "Actions", "Действия");
            BottomButton(more, footer.transform, 400, 140);
            var pause = Button(campaign, "pauseButton");
            BottomButton(pause, footer.transform, 0, 130);
            var pr = pause.GetComponent<RectTransform>();
            pr.anchorMin = pr.anchorMax = pr.pivot = new Vector2(1, 0);
            pr.anchoredPosition = new Vector2(-20, 14);
            var hint = LabelObject(desk, "hint");
            hint.transform.SetParent(footer.transform, false);
            Bottom(hint.GetComponent<RectTransform>(), new Vector2(565, 24), new Vector2(900, 30));
            Text(hint, 20);
            var controls = new[]
            {
                Button(campaign, "archiveButton"),
                Button(campaign, "courierButton"),
                Button(operations, "openUpgradesButton"),
                Button(operations, "openHistoryButton"),
                Button(investigation, "journalButton"),
                Button(desk, "menuButton")
            };
            for (int i = 0; i < controls.Length; i++)
            {
                Place(controls[i].gameObject, drawer.transform, new Vector2(10, -10 - i * 65), new Vector2(320, 55));
                Caption(controls[i]);
            }

            drawer.SetActive(false);
            var banner = (GameObject)campaign.FindProperty("callBanner").objectReferenceValue;
            Place(banner, canvas, Vector2.zero, new Vector2(370, 115));
            Right(banner.GetComponent<RectTransform>(), new Vector2(-20, -105));
            var callText = LabelObject(campaign, "callText");
            Place(callText, banner.transform, new Vector2(10, -8), new Vector2(350, 35));
            Text(callText, 22);
            var answer = Button(campaign, "answerButton");
            Place(answer.gameObject, banner.transform, new Vector2(10, -53), new Vector2(350, 50));
            Caption(answer);
            var guide = UnityEngine.Object.FindFirstObjectByType<TutorialGuide>();
            var panel = Find("Tutorial instructions").gameObject;
            Place(panel, canvas, new Vector2(20, -105), new Vector2(650, 58));
            var instruction = panel.transform.Find("Current step").gameObject;
            Place(instruction, panel.transform, new Vector2(12, -12), new Vector2(430, 36));
            Text(instruction, 23);
            var focus = panel.GetComponentsInChildren<Button>(true).First();
            Place(focus.gameObject, panel.transform, new Vector2(450, -8), new Vector2(135, 42));
            Caption(focus);
            focus.GetComponentInChildren<TMP_Text>(true).text = "К цели";
            var help = NewButton(panel.transform, "Tutorial help", "?");
            Place(help.gameObject, panel.transform, new Vector2(592, -8), new Vector2(48, 42));
            Caption(help);
            var details = Panel(panel.transform, "Tutorial details", new Vector2(650, 175));
            Place(details, panel.transform, new Vector2(0, -66), new Vector2(650, 175));
            var detailText = Label(details.transform, "Explanation");
            Place(detailText.gameObject, details.transform, new Vector2(14, -12), new Vector2(622, 150));
            detailText.text = "Подробная подсказка текущего шага";
            detailText.fontSize = 23;
            guide.ConfigureCompact(help, details, detailText);
            details.SetActive(false);
            EditorUtility.SetDirty(guide);
            var compact = Find("_Managers").GetComponent<CompactHud>() ?? Find("_Managers").gameObject.AddComponent<CompactHud>();
            compact.Configure(header, footer, drawer, more, back, next, submit, end);
            EditorUtility.SetDirty(compact);
            PrefabUtility.SaveAsPrefabAssetAndConnect(canvas.gameObject, "Assets/Prefabs/UI/GameInterface.prefab", InteractionMode.AutomatedAction);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            if (!string.IsNullOrEmpty(previous))
            {
                EditorSceneManager.OpenScene(previous);
            }

            Debug.Log("Z57_COMPACT_HUD_AUTHORED: contextual controls, compact bars, optional tutorial details and action drawer.");
        }

        private static GameObject Panel(Transform parent, string name, Vector2 size)
        {
            var existing = parent.Find(name);
            if (existing != null)
            {
                return existing.gameObject;
            }

            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            go.GetComponent<RectTransform>().sizeDelta = size;
            go.GetComponent<Image>().color = new Color(.9f, .9f, .9f, .96f);
            return go;
        }

        private static TMP_Text Label(Transform parent, string name)
        {
            var text = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TMP_Text>();
            text.transform.SetParent(parent, false);
            text.font = Resources.Load<TMP_FontAsset>("PrototypeFont");
            text.color = Color.black;
            text.raycastTarget = false;
            return text;
        }

        private static Button NewButton(Transform parent, string name, string caption)
        {
            var existing = parent.Find(name);
            if (existing != null)
            {
                return existing.GetComponent<Button>();
            }

            var go = Panel(parent, name, new Vector2(140, 50));
            var image = go.GetComponent<Image>();
            image.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            image.type = Image.Type.Sliced;
            image.color = Color.white;
            var button = go.AddComponent<Button>();
            button.targetGraphic = image;
            button.colors = ColorBlock.defaultColorBlock;
            Label(go.transform, "Caption").text = caption;
            return button;
        }

        private static void Caption(Button button)
        {
            var text = button.GetComponentInChildren<TMP_Text>(true);
            var r = text.rectTransform;
            r.anchorMin = Vector2.zero;
            r.anchorMax = Vector2.one;
            r.pivot = Vector2.one * .5f;
            r.offsetMin = new Vector2(8, 4);
            r.offsetMax = new Vector2(-8, -4);
            text.color = Color.black;
            text.alignment = TextAlignmentOptions.Center;
            text.enableAutoSizing = true;
            text.fontSizeMin = 16;
            text.fontSizeMax = 22;
        }

        private static void BottomButton(Button button, Transform parent, float x, float width)
        {
            button.transform.SetParent(parent, false);
            Bottom(button.GetComponent<RectTransform>(), new Vector2(x, 14), new Vector2(width, 52));
            Caption(button);
        }

        private static void Text(GameObject go, int size)
        {
            var t = go.GetComponent<TMP_Text>();
            t.fontSize = size;
            t.enableAutoSizing = false;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.overflowMode = TextOverflowModes.Truncate;
        }

        private static void Place(GameObject go, Transform parent, Vector2 pos, Vector2 size)
        {
            go.transform.SetParent(parent, false);
            var r = go.GetComponent<RectTransform>();
            r.anchorMin = r.anchorMax = r.pivot = new Vector2(0, 1);
            r.anchoredPosition = pos;
            r.sizeDelta = size;
        }

        private static void Bottom(RectTransform rect, Vector2 pos, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.zero;
            rect.anchoredPosition = pos;
            rect.sizeDelta = size;
        }

        private static void Right(RectTransform rect, Vector2 pos)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one;
            rect.anchoredPosition = pos;
        }

        private static void Stretch(RectTransform rect, bool top, float height)
        {
            rect.anchorMin = new Vector2(0, top ? 1 : 0);
            rect.anchorMax = new Vector2(1, top ? 1 : 0);
            rect.pivot = new Vector2(.5f, top ? 1 : 0);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(0, height);
        }
    }
}
