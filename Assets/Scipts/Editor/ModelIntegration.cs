using System.IO;
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
    public static class ModelIntegration
    {
        [MenuItem("Zvezda57/Integrate office models and prototype UI")]
        public static void Apply()
        {
            if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().isDirty)
            {
                throw new System.InvalidOperationException("Save the current scene before integration.");
            }

            Directory.CreateDirectory("Assets/Materials/Office");
            Directory.CreateDirectory("Assets/Prefabs/Office");
            var previous = UnityEngine.SceneManagement.SceneManager.GetActiveScene().path;
            var scene = EditorSceneManager.OpenScene(SceneNavigation.GamePath);
            var root = GameObject.Find("_World").transform;
            foreach (var light in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
            {
                light.shadows = LightShadows.None;
            }

            var rig = Object.FindFirstObjectByType<Zvezda57.CameraSystem.CameraRig>();
            var serializedRig = new SerializedObject(rig);
            var points = serializedRig.FindProperty("focusPoints");
            for (int i = 0; i < points.arraySize; i++)
            {
                var point = points.GetArrayElementAtIndex(i);
                int mode = point.FindPropertyRelative("Mode").intValue;
                if (mode == 2 || mode == 5)
                {
                    point.FindPropertyRelative("Position").vector3Value = new Vector3(0, 4.5f, -2.3f);
                }
            }

            serializedRig.ApplyModifiedPropertiesWithoutUndo();
            Attach("Table I", GameObject.Find("Desk"), new Vector3(3.7f, 1.025f, 2.2f), new Vector3(0, 0, 0));
            Attach("Table Ura", GameObject.Find("Yura desk"), new Vector3(1.1f, .875f, .7f), new Vector3(2.9f, 0, 1.6f));
            Place("Chair Ura", root, new Vector3(.7f, 1.1f, .7f), new Vector3(2.9f, 0, 2.28f));
            Attach("Recep", GameObject.Find("IntakeTray"), new Vector3(2.9f, 1.175f, .8f), new Vector3(0, 0, 2.6f));
            Place("Base", root, new Vector3(9, 4, 9), new Vector3(0, -.2f, 1));
            Place("Back wall", root, new Vector3(9, 4, .2f), new Vector3(0, 0, 4));
            foreach (string name in new[]
            {
                "Floor",
                "Back wall",
                "Left wall",
                "Desk left leg",
                "Desk right leg",
                "VisitorWindow"
            })
            {
                var go = GameObject.Find(name);
                if (go != null && go.GetComponent<Renderer>() != null)
                {
                    go.GetComponent<Renderer>().enabled = false;
                }
            }

            var canvas = Find("CanvasHUD").transform;
            AddGuide(canvas);
            Style(canvas.gameObject);
            SaveUI(canvas.gameObject, "Assets/Prefabs/UI/GameInterface.prefab");
            EditorSceneManager.SaveScene(scene);
            scene = EditorSceneManager.OpenScene(SceneNavigation.MenuPath);
            var menu = Find("MenuCanvas");
            Style(menu);
            SaveUI(menu, "Assets/Prefabs/UI/MainMenu.prefab");
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            if (!string.IsNullOrEmpty(previous))
            {
                EditorSceneManager.OpenScene(previous);
            }

            Debug.Log("Z57_MODELS_UI_PASS: six models, URP materials, editable tutorial and plain Unity UI saved.");
        }

        private static GameObject Find(string name) => Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None).First(x => x.name == name).gameObject;
        private static void Attach(string model, GameObject anchor, Vector3 size, Vector3 bottom)
        {
            Place(model, anchor.transform, size, bottom);
            anchor.GetComponent<Renderer>().enabled = false;
        }

        private static void Place(string model, Transform parent, Vector3 size, Vector3 bottom)
        {
            string name = model + " model";
            if (parent.Find(name) != null)
            {
                return;
            }

            var asset = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Models/Office/" + model + ".fbx");
            var container = new GameObject(name);
            container.transform.SetParent(parent, false);
            container.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            container.transform.localScale = Vector3.one;
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(asset, container.transform);
            instance.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.Euler(0, model == "Back wall" || model == "Recep" || model == "Table Ura" ? 90 : 0, 0));
            instance.transform.localScale = Vector3.one;
            var renderers = instance.GetComponentsInChildren<Renderer>();
            var bounds = renderers[0].bounds;
            foreach (var r in renderers)
            {
                bounds.Encapsulate(r.bounds);
            }

            var scale = new Vector3(size.x / bounds.size.x, size.y / bounds.size.y, size.z / bounds.size.z);
            container.transform.localScale = Vector3.Scale(container.transform.localScale, scale);
            bounds = renderers[0].bounds;
            foreach (var r in renderers)
            {
                bounds.Encapsulate(r.bounds);
            }

            container.transform.position += bottom - new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
            foreach (var r in renderers)
            {
                r.sharedMaterials = r.sharedMaterials.Select(m => Material(model, m.name)).ToArray();
            }

            container.transform.SetParent(null, true);
            PrefabUtility.SaveAsPrefabAssetAndConnect(container, "Assets/Prefabs/Office/" + model + ".prefab", InteractionMode.AutomatedAction);
            container.transform.SetParent(parent, true);
            PrefabUtility.RecordPrefabInstancePropertyModifications(container.transform);
        }

        private static Material Material(string model, string name)
        {
            string path = "Assets/Materials/Office/" + model + "_" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null)
            {
                return material;
            }

            material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            material.name = model + " " + name;
            string folder = "Assets/Textures/Office/" + (model == "Back wall" ? "Base" : model == "Recep" ? "Recep" : model);
            Texture2D Texture(string suffix) => AssetDatabase.LoadAssetAtPath<Texture2D>(folder + "/" + name + "_" + suffix + "_1001.png");
            material.SetTexture("_BaseMap", Texture("Base_color"));
            material.SetTexture("_BumpMap", Texture("Normal_OpenGL"));
            if (material.GetTexture("_BumpMap") != null)
            {
                material.EnableKeyword("_NORMALMAP");
            }

            material.SetFloat("_Smoothness", .18f);
            material.SetTexture("_OcclusionMap", Texture("Mixed_AO"));
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static void SaveUI(GameObject go, string path) => PrefabUtility.SaveAsPrefabAssetAndConnect(go, path, InteractionMode.AutomatedAction);
        private static void Style(GameObject canvas)
        {
            var sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            foreach (var image in canvas.GetComponentsInChildren<Image>(true))
            {
                if (image.name == "Inspection light")
                {
                    continue;
                }

                var button = image.GetComponent<Button>();
                image.sprite = button != null ? sprite : null;
                image.type = button != null ? Image.Type.Sliced : Image.Type.Simple;
                image.color = button != null ? Color.white : new Color(.87f, .87f, .87f, .98f);
                if (button != null)
                {
                    button.colors = ColorBlock.defaultColorBlock;
                    button.transition = Selectable.Transition.ColorTint;
                }
            }

            var font = Resources.Load<TMP_FontAsset>("PrototypeFont");
            foreach (var text in canvas.GetComponentsInChildren<TMP_Text>(true))
            {
                text.font = font;
                text.color = Color.black;
                text.fontStyle = FontStyles.Normal;
                if (text.GetComponentInParent<Button>() != null)
                {
                    text.alignment = TextAlignmentOptions.Center;
                    text.enableAutoSizing = true;
                    text.fontSizeMin = 16;
                    text.fontSizeMax = 24;
                }
                else if (text.fontSize > 40)
                {
                    text.fontSize = 40;
                }
            }

            if (canvas.name == "CanvasHUD")
            {
                Backdrop(canvas.transform, "Prototype left sidebar", new Vector2(0, 0), new Vector2(390, 1080));
                Backdrop(canvas.transform, "Prototype header", new Vector2(390, 0), new Vector2(860, 185));
                Backdrop(canvas.transform, "Prototype right sidebar", new Vector2(1250, 0), new Vector2(670, 300));
                Backdrop(canvas.transform, "Prototype tool sidebar", new Vector2(1500, -300), new Vector2(420, 650));
                Backdrop(canvas.transform, "Prototype footer", new Vector2(390, -970), new Vector2(1530, 110));
            }
        }

        private static GameObject Backdrop(Transform parent, string name, Vector2 position, Vector2 size)
        {
            var existing = parent.Find(name);
            if (existing != null)
            {
                return existing.gameObject;
            }

            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            go.GetComponent<Image>().color = new Color(.88f, .88f, .88f, .94f);
            go.GetComponent<Image>().raycastTarget = false;
            go.transform.SetAsFirstSibling();
            return go;
        }

        private static void AddGuide(Transform canvas)
        {
            if (Find("_Managers").GetComponent<TutorialGuide>() != null)
            {
                return;
            }

            var panel = Backdrop(canvas, "Tutorial instructions", new Vector2(1505, -535), new Vector2(390, 430));
            panel.transform.SetAsLastSibling();
            var label = new GameObject("Current step", typeof(RectTransform), typeof(TextMeshProUGUI));
            label.transform.SetParent(panel.transform, false);
            var rect = label.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(15, -15);
            rect.sizeDelta = new Vector2(360, 315);
            var text = label.GetComponent<TMP_Text>();
            text.font = Resources.Load<TMP_FontAsset>("PrototypeFont");
            text.fontSize = 25;
            text.text = "1/7 · Получите учебную папку";
            text.raycastTarget = false;
            var buttonGO = new GameObject("Show tutorial target", typeof(RectTransform), typeof(Image), typeof(Button));
            buttonGO.transform.SetParent(panel.transform, false);
            var br = buttonGO.GetComponent<RectTransform>();
            br.anchorMin = br.anchorMax = br.pivot = new Vector2(0, 1);
            br.anchoredPosition = new Vector2(15, -360);
            br.sizeDelta = new Vector2(360, 50);
            var caption = Object.Instantiate(label, buttonGO.transform);
            caption.name = "Caption";
            caption.GetComponent<RectTransform>().anchoredPosition = Vector2.zero;
            caption.GetComponent<RectTransform>().sizeDelta = br.sizeDelta;
            caption.GetComponent<TMP_Text>().text = "Показать цель";
            var arrow = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Models/Tutorial/Arrow.obj"));
            arrow.name = "Tutorial world arrow";
            arrow.transform.SetParent(GameObject.Find("_World").transform);
            arrow.transform.localScale = Vector3.one * .55f;
            var mat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            mat.color = new Color(1, .72f, 0);
            AssetDatabase.CreateAsset(mat, "Assets/Materials/Office/TutorialArrow.mat");
            foreach (var r in arrow.GetComponentsInChildren<Renderer>())
            {
                r.sharedMaterial = mat;
            }

            var pointer = Object.Instantiate(label, canvas);
            pointer.name = "Tutorial button pointer";
            pointer.GetComponent<TMP_Text>().text = "<";
            pointer.GetComponent<TMP_Text>().fontSize = 40;
            pointer.GetComponent<RectTransform>().sizeDelta = new Vector2(50, 50);
            var guide = Find("_Managers").AddComponent<TutorialGuide>();
            RectTransform Button(string name) => Find(name).GetComponent<RectTransform>();
            var office = new SerializedObject(Object.FindAnyObjectByType<OfficePrototype>());
            var firstDocument = office.FindProperty("documentSlots").GetArrayElementAtIndex(0).objectReferenceValue as Component;
            var deskHud = new SerializedObject(Object.FindAnyObjectByType<DeskHud>());
            var closeReader = (Button)deskHud.FindProperty("closeReaderButton").objectReferenceValue;
            guide.Configure(panel, text, buttonGO.GetComponent<Button>(), arrow.transform, pointer.GetComponent<RectTransform>(),
                new[] { Find("CaseFolder").transform, firstDocument.transform, Find("Stamp Approve").transform, Find("Ink pad").transform,
                    Find("CityMap").transform, Find("Crew plug Repair").transform, Find("Sector socket 2").transform },
                new[] { Button("File case"), Button("Call visitor"), Button("End shift"), closeReader.GetComponent<RectTransform>() });
            arrow.SetActive(false);
            pointer.SetActive(false);
        }
    }
}
