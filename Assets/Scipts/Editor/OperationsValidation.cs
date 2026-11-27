using System;
using System.IO;
using System.Linq;
using UnityEngine;
using Zvezda57.Dispatch;
using Zvezda57.Systems;

namespace Zvezda57.Editor
{
    public static class OperationsValidation
    {
        private static void Require(bool value, string message)
        {
            if (!value)
            {
                throw new Exception(message);
            }
        }

        public static void Run()
        {
            if (!Application.isBatchMode)
            {
                throw new InvalidOperationException("Use an isolated Editor.");
            }

            var metrics = new CityMetrics();
            metrics.Apply("a", "Допуск", new MetricDelta(-150, 40, -10));
            Require(metrics.Reactor == 0 && metrics.Secrecy == 100 && metrics.Calm == 65, "City metrics clamp independently");
            Require(!metrics.Apply("a", "Повтор", new MetricDelta(30, 0, 0)) && metrics.History.Count == 1, "Duplicate consequence rejected");
            var chain = ScriptableObject.CreateInstance<IncidentDefinition>();
            chain.Id = "chain";
            chain.TravelTime = .2f;
            chain.ReturnTime = 2;
            chain.Stages = new[]
            {
                new IncidentStage
                {
                    Title = "Fire",
                    AllowedCrews = new[]
                    {
                        CrewType.Fire
                    },
                    Deadline = 10,
                    WorkTime = .2f
                },
                new IncidentStage
                {
                    Title = "Repair",
                    AllowedCrews = new[]
                    {
                        CrewType.Repair
                    },
                    Deadline = 10,
                    WorkTime = .2f
                }
            };
            var fail = ScriptableObject.CreateInstance<IncidentDefinition>();
            fail.Id = "fail";
            fail.AllowedCrews = new[]
            {
                CrewType.Repair
            };
            fail.Deadline = .2f;
            fail.OnFailureIncidentId = "chain";
            var second = ScriptableObject.CreateInstance<IncidentDefinition>();
            second.Id = "second";
            second.AllowedCrews = new[]
            {
                CrewType.Ops
            };
            second.Deadline = 35;
            var first = ScriptableObject.CreateInstance<IncidentDefinition>();
            first.Id = "first";
            first.AllowedCrews = new[]
            {
                CrewType.Ops
            };
            first.Deadline = 50;
            first.TravelTime = 10;
            var content = UnityEditor.AssetDatabase.FindAssets("t:UpgradeDefinition", new[] { "Assets/Settings/Upgrades" }).Select(x => UnityEditor.AssetDatabase.LoadAssetAtPath<UpgradeDefinition>(UnityEditor.AssetDatabase.GUIDToAssetPath(x))).ToArray();
            var path = Path.Combine(Path.GetTempPath(), "Z57Upgrades-" + Guid.NewGuid() + ".json");
            UpgradeService upgrades = null;
            try
            {
                var routing = new DispatchService(new[] { first, chain, second });
                routing.Trigger(first.Id);
                routing.Trigger(chain.Id);
                routing.Trigger(second.Id);
                Require(routing.DispatchToSector(0, first.Sector, out _), "Sector dispatch finds a compatible call");
                Require(routing.Incidents[0].State == IncidentState.Waiting &&
                    routing.Incidents[1].State == IncidentState.Waiting &&
                    routing.Incidents[2].State == IncidentState.Assigned, "Sector dispatch prioritizes the shortest compatible deadline");
                Require(!routing.DispatchToSector(0, first.Sector, out _), "Sector dispatch does not reuse a busy crew");
                Require(!routing.DispatchToSector(1, first.Sector, out _), "Sector dispatch rejects an incompatible crew");
                Require(!routing.DispatchToSector(2, first.Sector + 1, out _), "Sector dispatch ignores incidents in other sectors");

                var service = new DispatchService(new[] { chain, fail, first, second });
                service.Start();
                int outcomes = 0;
                service.Finished += x => outcomes++;
                service.Trigger("chain");
                service.Dispatch(CrewType.Fire, "chain", out _);
                service.Tick(.5f);
                Require(service.Incidents[0].StageIndex == 1 && service.Incidents[0].State == IncidentState.Waiting && outcomes == 0, "Stage one does not resolve chain");
                Require(service.Crews[2].State == CrewState.Return, "First crew stays busy returning");
                Require(!service.Dispatch(CrewType.Fire, "chain", out _) && service.Dispatch(CrewType.Repair, "chain", out _), "Second stage requires repair");
                service.Tick(.5f);
                Require(service.Incidents[0].State == IncidentState.Resolved && outcomes == 1, "Final stage resolves once");
                service.Tick(3);
                Require(outcomes == 1, "No duplicate outcome on return");
                var escalation = new DispatchService(new[] { chain, fail });
                escalation.Start();
                escalation.Trigger("fail");
                int failures = 0;
                escalation.Finished += _ => failures++;
                Require(!escalation.Dispatch(CrewType.Guard, "fail", out _), "Wrong dispatch can exhaust the deadline");
                Require(escalation.Incidents[0].State == IncidentState.Waiting &&
                    escalation.Incidents[1].State == IncidentState.Failed && failures == 1,
                    "Failure starts configured escalation and reports the outcome once");
                upgrades = new UpgradeService(content, service, path);
                upgrades.Reward("test", 5);
                upgrades.Reward("test", 5);
                Require(upgrades.Points == 5, "Reward cannot be farmed");
                upgrades.Toggle("UP_OPS", out _);
                Require(service.Crews.Count == 4, "Draft does not add crew");
                upgrades.Cancel();
                Require(upgrades.DraftCost == 0, "Draft can be cancelled");
                upgrades.Toggle("UP_OPS", out _);
                upgrades.Toggle("UP_VEHICLE", out _);
                Require(!upgrades.Toggle("UP_FOAM", out _), "Overspending rejected");
                Require(upgrades.Commit(out _) && upgrades.Points == 0 && service.Crews.Count == 5, "Commit spends exact points and adds crew");
                Require(!upgrades.Toggle("UP_OPS", out _), "Purchased upgrade cannot be repeated");
                service.Trigger("first");
                service.Trigger("second");
                Require(service.Dispatch(0, "first", out _) && service.Dispatch(4, "second", out _), "Extra ops crew handles independent parallel call");
                Require(Math.Abs(service.Crews[0].Remaining - 8) < .01f, "Vehicle reduces travel by 20 percent");
                var restoredDispatch = new DispatchService(new[] { first });
                var restored = new UpgradeService(content, restoredDispatch, path);
                Require(restored.Points == 0 && restored.Owns("UP_OPS") && restoredDispatch.Crews.Count == 5 && Math.Abs(restoredDispatch.TravelMultiplier - .8f) < .001f, "Purchases persist and apply on reload");
                upgrades.Reward("additional-test", 3);
                upgrades.Toggle("UP_FOAM", out _);
                upgrades.Toggle("UP_RADIO", out _);
                upgrades.Commit(out _);
                Require(Math.Abs(service.FireWorkMultiplier - .75f) < .001f && service.EarlyWarningSeconds == 15, "Foam and radio effects applied");
                Debug.Log("Z57_OPERATIONS_PASS: metric clamp/idempotence; sector routing; multi-stage requirements, busy return, final outcome, failure escalation; draft/cancel/budget/commit; parallel extra crew; vehicle/foam/radio; saved purchases.");
            }
            finally
            {
                upgrades?.Clear();
                foreach (var item in new[]
                {
                    chain,
                    fail,
                    first,
                    second
                })
                {
                    UnityEngine.Object.DestroyImmediate(item);
                }
            }

            FeatureValidation.Run();
        }
    }
}
