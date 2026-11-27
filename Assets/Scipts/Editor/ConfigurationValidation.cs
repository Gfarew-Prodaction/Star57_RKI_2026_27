using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using VContainer;
using Zvezda57.CameraSystem;
using Zvezda57.Core;
using Zvezda57.Dispatch;
using Zvezda57.InputSystem;
using Zvezda57.Systems;

namespace Zvezda57.Editor
{
    public static class ConfigurationValidation
    {
        private const string RunningKey = "Z57.ConfigurationValidation";
        private const string SavePathKey = "Z57.ManualSavePath";
        private const string PumpId = "DEMO.LIGHTS";
        private const string CampaignPath = "Assets/Settings/Shifts/Campaign.asset";
        private const string ExamplePath = "Assets/Settings/Examples/Campaign_WithPumpOutage.asset";

        private static CampaignDefinition s_campaign;
        private static ShiftDefinition s_originalShift;
        private static ShiftDefinition s_exampleShift;
        private static float s_exampleDelay;
        private static Dictionary<string, byte[]> s_saveFiles;
        private static int s_step;
        private static double s_due;
        private static double s_timeout;
        private static int s_calmBefore;

        [InitializeOnLoadMethod]
        private static void Subscribe()
        {
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        public static void Run()
        {
            if (!Application.isBatchMode)
            {
                throw new InvalidOperationException("Run in an isolated batch Editor.");
            }

            EditorSceneManager.OpenScene(SceneNavigation.GamePath);
            var scope = UnityEngine.Object.FindFirstObjectByType<OfficeLifetimeScope>();
            var baseline = scope.ValidateConfiguration();
            Require(baseline.IsValid, string.Join("\n", baseline.Errors));

            var example = AssetDatabase.LoadAssetAtPath<CampaignDefinition>(ExamplePath);
            scope.ConfigureCampaign(example);
            var expanded = scope.ValidateConfiguration();
            Require(expanded.IsValid, string.Join("\n", expanded.Errors));
            Require(expanded.Incidents.Count == baseline.Incidents.Count + 1, "One reference adds one catalog entry.");
            CheckBoundaries(scope, example.Shifts[0]);

            string directory = Path.Combine(Path.GetTempPath(), "Z57-config-" + Guid.NewGuid());
            Directory.CreateDirectory(directory);
            SessionState.SetString(SavePathKey, Path.Combine(directory, "decisions.json"));
            EditorSceneManager.OpenScene(SceneNavigation.MenuPath);
            SessionState.SetBool(RunningKey, true);
            EditorApplication.EnterPlaymode();
        }

        private static void CheckBoundaries(OfficeLifetimeScope scope, ShiftDefinition shift)
        {
            var schedule = shift.Incidents;
            var entry = schedule.Last();
            var incident = entry.Incident;
            int sector = incident.Sector;
            float delay = entry.Delay;
            var crews = incident.AllowedCrews;
            try
            {
                entry.Delay = 0;
                incident.Sector = 6;
                Require(scope.ValidateConfiguration().IsValid, "Zero delay and sector 6 are valid.");

                entry.Delay = float.PositiveInfinity;
                incident.Sector = 7;
                incident.AllowedCrews = Array.Empty<CrewType>();
                shift.Incidents = schedule.Concat(new[]
                {
                    entry,
                    new ScheduledIncident { Id = "UNKNOWN" }
                }).ToArray();

                var invalid = scope.ValidateConfiguration();
                foreach (string field in new[] { "Delay", "Sector", "AllowedCrews", "already scheduled", "UNKNOWN" })
                {
                    Require(invalid.Errors.Any(error => error.Contains(field)), "Missing diagnostic for " + field);
                }
            }
            finally
            {
                incident.Sector = sector;
                incident.AllowedCrews = crews;
                entry.Delay = delay;
                shift.Incidents = schedule;
            }
        }

        private static void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(RunningKey, false))
            {
                return;
            }

            s_due = EditorApplication.timeSinceStartup + .5;
            s_timeout = s_due + 90;
            EditorApplication.update += Advance;
        }

        private static void Advance()
        {
            if (EditorApplication.timeSinceStartup < s_due)
            {
                return;
            }

            try
            {
                Require(EditorApplication.timeSinceStartup < s_timeout, $"Configuration validation timed out at step {s_step}.");
                if (SceneNavigation.IsLoading)
                {
                    return;
                }

                if (SceneManager.GetActiveScene().path == SceneNavigation.MenuPath)
                {
                    AdvanceMenu();
                }
                else if (SceneManager.GetActiveScene().path == SceneNavigation.GamePath)
                {
                    AdvanceGame();
                }

                s_due = EditorApplication.timeSinceStartup + .75;
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                Finish(1);
            }
        }

        private static void AdvanceMenu()
        {
            if (s_step == 0)
            {
                CheckHierarchyNames();
                Require(GameObject.Find("New game").GetComponentInChildren<TMP_Text>().text == "Новая игра",
                    "The menu caption remains Russian.");
                SceneNavigation.OpenGame(true);
                s_step = 1;
            }
            else if (s_step == 3)
            {
                s_saveFiles = ReadSaveFiles();
                Require(s_saveFiles.Count > 0, "Expected progress before invalid new game attempt.");
                s_campaign = AssetDatabase.LoadAssetAtPath<CampaignDefinition>(CampaignPath);
                s_originalShift = s_campaign.Shifts[0];
                s_exampleShift = AssetDatabase.LoadAssetAtPath<CampaignDefinition>(ExamplePath).Shifts[0];
                s_campaign.Shifts[0] = s_exampleShift;

                var entry = s_exampleShift.Incidents.Last();
                s_exampleDelay = entry.Delay;
                entry.Delay = -1;
                SceneNavigation.OpenGame(true);
                s_step = 4;
            }
            else if (s_step == 4)
            {
                Require(UnityEngine.Object.FindObjectsByType<TMP_Text>(FindObjectsSortMode.None)
                    .Any(label => label.text.StartsWith("Не удалось начать смену", StringComparison.Ordinal)), "Menu displays a validation error.");
                var files = ReadSaveFiles();
                Require(files.Count == s_saveFiles.Count &&
                    s_saveFiles.All(file => files.TryGetValue(file.Key, out var contents) && file.Value.SequenceEqual(contents)),
                    "Rejected new game changed existing saves.");
                Require(GameObject.Find("Continue").GetComponent<Button>().interactable, "Existing progress remains available.");
                s_exampleShift.Incidents.Last().Delay = s_exampleDelay;
                SceneNavigation.OpenGame(false);
                s_step = 5;
            }
        }

        private static void AdvanceGame()
        {
            if (s_step == 4)
            {
                return;
            }

            var container = UnityEngine.Object.FindFirstObjectByType<OfficeLifetimeScope>().Container;
            var campaign = container.Resolve<ICampaignService>();
            var dispatch = container.Resolve<IDispatchService>();
            var rig = UnityEngine.Object.FindFirstObjectByType<CameraRig>();
            var sector = GameObject.Find("Sector socket 5").GetComponent<WorldAction>();

            switch (s_step)
            {
                case 1:
                    Require(dispatch.Incidents.All(item => item.Id != PumpId), "Baseline has no pump call.");
                    Require(campaign.Choose(0), "Baseline starts after briefing.");
                    campaign.Tick(6);
                    rig.Go(CameraMode.Map);
                    break;
                case 2:
                    sector.Use();
                    Capture("config-before");
                    SceneNavigation.OpenMenu();
                    break;
                case 5:
                    Require(campaign.CanInteract, "Old save resumes with the example installed.");
                    campaign.Tick(1);
                    rig.Go(CameraMode.Map);
                    break;
                case 6:
                    Require(Pump(dispatch).State == IncidentState.Waiting, "New call appears from shift data on the existing save.");
                    sector.Use();
                    Require(UnityEngine.Object.FindObjectsByType<TMP_Text>(FindObjectsSortMode.None)
                        .Any(label => label.text.Contains("Погас свет на насосной")), "Sector panel displays the new call.");
                    CheckHierarchyNames();
                    Capture("config-after");
                    GameObject.Find("Close").GetComponent<Button>().onClick.Invoke();
                    s_calmBefore = container.Resolve<ICityMetrics>().Calm;
                    GameObject.Find("Crew plug Repair").GetComponent<CrewPlug>().Use();
                    sector.Use();
                    Require(Pump(dispatch).State == IncidentState.Assigned, "Existing map controls assign the repair crew.");
                    Capture("config-assigned");
                    break;
                case 7:
                    campaign.Tick(15);
                    Require(Pump(dispatch).State == IncidentState.Resolved, "Crew completes the new call.");
                    Require(container.Resolve<ICityMetrics>().Calm == s_calmBefore + 3, "Success effect comes from the new asset.");
                    sector.Use();
                    Capture("config-resolved");
                    Debug.Log("Z57_CONFIGURATION_PASS: stock assets; invalid and boundary values; rejected new game preserves saves; data-only incident and dispatch on resumed save.");
                    Finish(0);
                    return;
            }

            s_step++;
        }

        private static IIncidentStatus Pump(IDispatchService dispatch)
        {
            return dispatch.Incidents.Single(item => item.Id == PumpId);
        }

        private static Dictionary<string, byte[]> ReadSaveFiles()
        {
            return Directory.GetFiles(Path.GetDirectoryName(SavePaths.Decisions)).ToDictionary(path => path, File.ReadAllBytes);
        }

        private static void CheckHierarchyNames()
        {
            var nonEnglishName = SceneManager.GetActiveScene().GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
                .FirstOrDefault(item => Regex.IsMatch(item.name, @"\p{IsCyrillic}"));
            Require(nonEnglishName == null, $"Scene object has a Russian name: {nonEnglishName?.name}");
        }

        private static void Capture(string name)
        {
            if (SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null)
            {
                PrototypeVisualCheck.Capture(name);
            }
        }

        private static void Require(bool condition, string message)
        {
            if (!condition)
            {
                throw new InvalidOperationException(message);
            }
        }

        private static void Finish(int code)
        {
            EditorApplication.update -= Advance;
            SessionState.EraseBool(RunningKey);
            if (s_exampleShift != null)
            {
                s_exampleShift.Incidents.Last().Delay = s_exampleDelay;
                s_campaign.Shifts[0] = s_originalShift;
            }

            EditorApplication.Exit(code);
        }
    }
}
