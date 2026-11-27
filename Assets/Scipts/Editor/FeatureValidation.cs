using System;
using System.IO;
using System.Linq;
using UnityEditor.SceneManagement;
using UnityEditor;
using UnityEngine.UI;
using UnityEngine;
using VContainer;
using Zvezda57.CameraSystem;
using Zvezda57.Core;
using Zvezda57.Dispatch;
using Zvezda57.Documents;
using Zvezda57.InputSystem;
using Zvezda57.Systems;

namespace Zvezda57.Editor
{
    public static class FeatureValidation
    {
        private static void Require(bool condition, string text)
        {
            if (!condition)
            {
                throw new Exception(text);
            }
        }

        public static void Run()
        {
            if (!Application.isBatchMode)
            {
                throw new InvalidOperationException("Use an isolated batch Editor.");
            }

            DomainChecks();
            CampaignValidation.Run();
        }

        private static void DomainChecks()
        {
            string path = Path.Combine(Path.GetTempPath(), "Z57-test-" + Guid.NewGuid() + ".json");
            var store = new JsonDecisionStore(path);
            try
            {
                var first = new DecisionRecord
                {
                    CaseId = "test",
                    Evidence = new[]
                    {
                        "clue"
                    },
                    Verdict = CaseVerdict.Recheck
                };
                store.Append(first);
                Require(new JsonDecisionStore(path).Records.Single().Evidence.Single() == "clue", "JSON round trip preserves evidence and verdict");
                bool duplicate = false;
                try
                {
                    store.Append(first);
                }
                catch (InvalidOperationException)
                {
                    duplicate = true;
                }

                Require(duplicate && store.Records.Count == 1, "Duplicate decisions rejected");
                store.Append(new DecisionRecord { CaseId = "test2" });
                Require(File.Exists(path + ".bak"), "Atomic save creates backup");
            }
            finally
            {
                store.Clear();
            }

            var repair = ScriptableObject.CreateInstance<IncidentDefinition>();
            repair.Id = "repair";
            repair.AllowedCrews = new[]
            {
                CrewType.Repair
            };
            repair.Deadline = 20;
            repair.TravelTime = 1;
            repair.WorkTime = 1;
            repair.ReturnTime = 1;
            var fire = ScriptableObject.CreateInstance<IncidentDefinition>();
            fire.Id = "fire";
            fire.AllowedCrews = new[]
            {
                CrewType.Fire
            };
            fire.Deadline = 20;
            fire.TravelTime = 1;
            fire.WorkTime = 1;
            fire.ReturnTime = 1;
            var missed = ScriptableObject.CreateInstance<IncidentDefinition>();
            missed.Id = "missed";
            missed.AllowedCrews = new[]
            {
                CrewType.Ops
            };
            missed.Deadline = 1;
            try
            {
                var service = new DispatchService(new[] { repair, fire, missed });
                service.Start();
                Require(service.Trigger("repair") && service.Trigger("fire"), "Two incidents coexist");
                Require(!service.Dispatch(CrewType.Guard, "repair", out _), "Guard cannot repair power");
                Require(service.Crews[(int)CrewType.Guard].State == CrewState.Return &&
                    service.Crews[(int)CrewType.Guard].Remaining == 8 &&
                    service.Incidents[0].State == IncidentState.Waiting &&
                    service.Incidents[0].Remaining == 10, "Wrong dispatch delays the crew and subtracts time from the call");
                var restored = new DispatchService(new[] { repair, fire, missed });
                restored.Restore(service.Capture());
                Require(restored.Crews[(int)CrewType.Guard].Remaining == 8 &&
                    restored.Incidents[0].Remaining == 10, "Wrong dispatch penalties survive a save");
                Require(service.Dispatch(CrewType.Repair, "repair", out _), "Repair assigned");
                Require(!service.Dispatch(CrewType.Repair, "fire", out _), "Busy crew rejected");
                Require(service.Dispatch(CrewType.Fire, "fire", out _), "Fire crew independently assigned");
                Require(!service.Trigger("repair"), "Duplicate incident trigger rejected");
                service.Tick(1.1f);
                Require(service.Crews[(int)CrewType.Repair].State == CrewState.Work, "Travel to work");
                service.Tick(1.1f);
                Require(service.Incidents[0].State == IncidentState.Resolved && service.Crews[(int)CrewType.Repair].State == CrewState.Return, "Work resolves and returns");
                service.Tick(1.1f);
                Require(service.Crews[(int)CrewType.Guard].State == CrewState.Return, "Wrong crew stays occupied during the penalty");
                service.Tick(5);
                Require(service.Crews.All(x => x.State == CrewState.Free), "Crews free after return");
                service.Trigger("missed");
                service.Tick(2);
                Require(service.Incidents[2].State == IncidentState.Failed, "Ignored incident escalates");
                repair.Deadline = 2;
                repair.WorkTime = 10;
                var longWork = new DispatchService(new[] { repair });
                longWork.Start();
                longWork.Trigger("repair");
                longWork.Dispatch(CrewType.Repair, "repair", out _);
                longWork.Tick(12);
                Require(longWork.Incidents[0].State == IncidentState.Resolved, "On-site work does not expire with arrival deadline");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(repair);
                UnityEngine.Object.DestroyImmediate(fire);
                UnityEngine.Object.DestroyImmediate(missed);
            }

            Debug.Log("Z57_DOMAIN_PASS: save/reload/duplicate, four crew lifecycle rules, incompatible crew, concurrent calls, deadlines.");
        }
    }
}
