using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor.SceneManagement;
using UnityEditor;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering;
using UnityEngine;
using Zvezda57.Core;
using Zvezda57.UI;
using Zvezda57.Rendering;

namespace Zvezda57.Editor
{
    public static class OfficeArtDirection
    {
        private static Transform Find(string name) => UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None).First(x => x.name == name);
        [MenuItem("Zvezda57/Apply office lighting and ink style")]
        public static void Apply()
        {
            if (EditorApplication.isPlaying || UnityEngine.SceneManagement.SceneManager.GetActiveScene().isDirty)
            {
                throw new InvalidOperationException("Save the scene and stop Play Mode first.");
            }

            var previous = UnityEngine.SceneManagement.SceneManager.GetActiveScene().path;
            Directory.CreateDirectory("Assets/Settings/Rendering");
            Directory.CreateDirectory("Assets/Materials/Rendering");
            var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>("Assets/Settings/PC_Renderer.asset");
            var ink = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Rendering/OfficeInk.mat");
            if (ink == null)
            {
                ink = new Material(Shader.Find("Zvezda57/Office Ink"));
                AssetDatabase.CreateAsset(ink, "Assets/Materials/Rendering/OfficeInk.mat");
            }

            var feature = renderer.rendererFeatures.OfType<InkOutlineFeature>().FirstOrDefault();
            if (feature == null)
            {
                feature = ScriptableObject.CreateInstance<InkOutlineFeature>();
                feature.name = "Office ink outlines";
                AssetDatabase.AddObjectToAsset(feature, renderer);
                renderer.rendererFeatures.Add(feature);
            }

            feature.Configure(ink);
            EditorUtility.SetDirty(feature);
            EditorUtility.SetDirty(renderer);
            var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>("Assets/Settings/PC_RPAsset.asset");
            pipeline.supportsCameraDepthTexture = true;
            pipeline.supportsHDR = true;
            pipeline.shadowDistance = 18;
            pipeline.msaaSampleCount = 1;
            EditorUtility.SetDirty(pipeline);
            GraphicsSettings.defaultRenderPipeline = pipeline;
            var scene = EditorSceneManager.OpenScene(SceneNavigation.GamePath);
            var camera = Camera.main;
            camera.GetUniversalAdditionalCameraData().renderPostProcessing = true;
            camera.GetUniversalAdditionalCameraData().antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.018f, .022f, .03f);
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(.25f, .26f, .29f);
            RenderSettings.ambientIntensity = 1;
            RenderSettings.reflectionIntensity = .22f;
            foreach (var light in UnityEngine.Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
            {
                if (light.type == LightType.Directional)
                {
                    light.intensity = .4f;
                    light.color = new Color(.9f, .9f, 1);
                    light.shadows = LightShadows.None;
                }
            }

            var lighting = GameObject.Find("Office lighting") ?? new GameObject("Office lighting");
            Light Lamp(string name, Vector3 position, LightType type, Color color, float intensity, float range, bool shadows)
            {
                var child = lighting.transform.Find(name);
                var go = child != null ? child.gameObject : new GameObject(name, typeof(Light));
                go.transform.SetParent(lighting.transform, false);
                go.transform.position = position;
                var l = go.GetComponent<Light>();
                l.type = type;
                l.color = color;
                l.intensity = intensity;
                l.range = range;
                l.shadows = shadows ? LightShadows.Soft : LightShadows.None;
                l.shadowBias = .025f;
                l.shadowNormalBias = .2f;
                return l;
            }

            var ceiling = Lamp("Ceiling warm pool", new Vector3(0, 3.12f, -.65f), LightType.Spot, new Color(1, .78f, .54f), 13, 8, true);
            ceiling.spotAngle = 135;
            ceiling.innerSpotAngle = 95;
            ceiling.transform.rotation = Quaternion.Euler(90, 0, 0);
            Lamp("Room bounced light", new Vector3(0, 2.65f, 1.6f), LightType.Point, new Color(1, .87f, .7f), 4, 7, false);
            Lamp("Window cool fill", new Vector3(3.9f, 2, .15f), LightType.Point, new Color(.67f, .78f, 1), 1.6f, 6, false);
            Lamp("Desk reflected light", new Vector3(-1.5f, 1.9f, -1.9f), LightType.Point, new Color(1, .74f, .49f), .8f, 4, false);
            Lamp("Reception corridor glow", new Vector3(-.6f, 2.2f, 4.05f), LightType.Point, new Color(.66f, .77f, 1), 1.8f, 3, false);
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>("Assets/Settings/Rendering/OfficeGrade.asset");
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, "Assets/Settings/Rendering/OfficeGrade.asset");
            }

            T Effect<T>()
                where T : VolumeComponent
            {
                if (!profile.TryGet<T>(out var effect))
                {
                    effect = profile.Add<T>(true);
                }

                return effect;
            }

            Effect<Tonemapping>().mode.Override(TonemappingMode.ACES);
            var grade = Effect<ColorAdjustments>();
            grade.postExposure.Override(.3f);
            grade.contrast.Override(8);
            grade.saturation.Override(-15);
            var vignette = Effect<Vignette>();
            vignette.intensity.Override(.18f);
            vignette.smoothness.Override(.65f);
            var bloom = Effect<Bloom>();
            bloom.intensity.Override(.08f);
            bloom.threshold.Override(1.3f);
            foreach (var c in profile.components)
            {
                if (!EditorUtility.IsPersistent(c))
                {
                    AssetDatabase.AddObjectToAsset(c, profile);
                }
            }

            EditorUtility.SetDirty(profile);
            var volume = lighting.GetComponent<Volume>() ?? lighting.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 20;
            volume.sharedProfile = profile;
            ArrangeYura();
            Find("Switchboard blockout").gameObject.SetActive(false);
            var wall = Box("Hallway backdrop", GameObject.Find("_World").transform, new Vector3(0, 1.8f, 4.4f), new Vector3(4, 3, .1f), new Color(.15f, .19f, .25f));
            wall.GetComponent<Renderer>().sharedMaterial.SetFloat("_Smoothness", 0);
            EditorUtility.SetDirty(wall.GetComponent<Renderer>().sharedMaterial);
            EditorSceneManager.SaveScene(scene);
            scene = EditorSceneManager.OpenScene(SceneNavigation.MenuPath);
            var menu = new SerializedObject(UnityEngine.Object.FindAnyObjectByType<MainMenu>());
            var status = (TMP_Text)menu.FindProperty("status").objectReferenceValue;
            status.text = "";
            PrefabUtility.RecordPrefabInstancePropertyModifications(status);
            EditorSceneManager.SaveScene(scene);
            var menuPrefab = PrefabUtility.LoadPrefabContents("Assets/Prefabs/UI/MainMenu.prefab");
            try
            {
                var text = menuPrefab.GetComponentsInChildren<TMP_Text>(true).First(x => x.rectTransform.anchoredPosition == new Vector2(205, -825));
                text.text = "";
                PrefabUtility.SaveAsPrefabAsset(menuPrefab, "Assets/Prefabs/UI/MainMenu.prefab");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(menuPrefab);
            }

            AssetDatabase.SaveAssets();
            if (!string.IsNullOrEmpty(previous))
            {
                EditorSceneManager.OpenScene(previous);
            }

            Debug.Log("Z57_ART_DIRECTION_PASS: ink render feature, authored lighting, grading and seated Yura saved.");
        }

        private static void ArrangeYura()
        {
            void Move(string name, Vector3 pos, Vector3 scale)
            {
                var t = Find(name);
                t.position = pos;
                t.localScale = scale;
                PrefabUtility.RecordPrefabInstancePropertyModifications(t);
            }

            Move("Yura body", new Vector3(2.9f, 1, 2.23f), new Vector3(.42f, .55f, .3f));
            Move("Yura head", new Vector3(2.9f, 1.44f, 2.2f), new Vector3(.28f, .32f, .28f));
            var root = Find("_WorldDevices");
            for (int i = 0; i < 2; i++)
            {
                float x = 2.9f + (i == 0 ? -.25f : .25f);
                Box("Yura forearm " + i, root, new Vector3(x, .98f, 1.98f), new Vector3(.12f, .13f, .43f), new Color(.24f, .31f, .26f));
                Box("Yura hand " + i, root, new Vector3(x, .98f, 1.73f), new Vector3(.13f, .12f, .14f), new Color(.65f, .58f, .44f));
                Box("Yura trouser " + i, root, new Vector3(2.9f + (i == 0 ? -.13f : .13f), .47f, 2.05f), new Vector3(.17f, .5f, .19f), new Color(.12f, .16f, .17f));
                Box("Yura shoe " + i, root, new Vector3(2.9f + (i == 0 ? -.13f : .13f), .16f, 1.98f), new Vector3(.18f, .16f, .32f), new Color(.06f, .06f, .06f));
            }

            var yura = new SerializedObject(UnityEngine.Object.FindAnyObjectByType<Zvezda57.Visitors.YuraPresenter>());
            var label = (TMP_Text)yura.FindProperty("label").objectReferenceValue;
            label.transform.SetParent(root, true);
            label.transform.SetPositionAndRotation(new Vector3(2.9f, 1.95f, 1.8f), Quaternion.identity);
            label.transform.localScale = Vector3.one;
            label.rectTransform.sizeDelta = new Vector2(1.5f, .35f);
            label.fontSize = 1.1f;
            label.text = "Юра";
            label.alignment = TextAlignmentOptions.Center;
            PrefabUtility.RecordPrefabInstancePropertyModifications(label.transform);
            PrefabUtility.RecordPrefabInstancePropertyModifications(label);
        }

        private static GameObject Box(string name, Transform parent, Vector3 pos, Vector3 scale, Color color)
        {
            var existing = parent.Find(name);
            if (existing != null)
            {
                return existing.gameObject;
            }

            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            go.transform.localScale = scale;
            var material = new Material(Shader.Find("Universal Render Pipeline/Lit"))
            {
                color = color
            };
            string path = "Assets/Materials/Rendering/" + name + ".mat";
            AssetDatabase.CreateAsset(material, path);
            go.GetComponent<Renderer>().sharedMaterial = material;
            UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
            return go;
        }
    }
}
