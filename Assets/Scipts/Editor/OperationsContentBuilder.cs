using System.IO;
using UnityEditor;
using UnityEngine;
using Zvezda57.Core;
using Zvezda57.Dispatch;
using Zvezda57.Documents;
using Zvezda57.Systems;

namespace Zvezda57.Editor
{
    public static class OperationsContentBuilder
    {
        public static void Configure(OfficeLifetimeScope scope, CaseDefinition[] cases, IncidentDefinition[] incidents)
        {
            foreach (var item in cases)
            {
                foreach (var rule in item.Verdicts)
                {
                    if (rule.Metrics.Reactor != 0 || rule.Metrics.Secrecy != 0 || rule.Metrics.Calm != 0)
                    {
                        continue;
                    }

                    rule.Metrics = rule.Accepted ? new MetricDelta(2, 2, 2) : new MetricDelta(0, -5, -5);
                    if (item.Id == "1.3" && rule.Verdict == CaseVerdict.Approve)
                    {
                        rule.Metrics = new MetricDelta(-10, -15, -5);
                    }

                    if (item.Id == "2.3" && rule.Verdict == CaseVerdict.Approve)
                    {
                        rule.Metrics = new MetricDelta(0, -25, -5);
                    }

                    if (item.Id == "2.2" && rule.Verdict == CaseVerdict.Detain)
                    {
                        rule.Metrics = new MetricDelta(0, -5, -15);
                    }

                    if (item.Id == "3.1" && rule.Verdict != CaseVerdict.Approve)
                    {
                        rule.Metrics = new MetricDelta(-20, 0, -5);
                    }
                }

                EditorUtility.SetDirty(item);
            }

            foreach (var item in incidents)
            {
                if (item.SuccessEffect.Reactor == 0 && item.SuccessEffect.Calm == 0 && item.SuccessEffect.Secrecy == 0)
                {
                    item.SuccessEffect = new MetricDelta(item.Sector == 4 ? 8 : 0, item.Sector == 1 || item.Sector == 6 ? 5 : 0, 5);
                    item.FailureEffect = new MetricDelta(item.Sector == 4 ? -20 : 0, item.Sector == 1 || item.Sector == 6 ? -15 : -5, -10);
                }

                item.EarlyWarningEligible = item.Id == "1.A" || item.Id == "2.A" || item.Id == "3.A";
                if (item.Id == "2.A" && item.Stages.Length == 0)
                {
                    item.Stages = new[]
                    {
                        new IncidentStage
                        {
                            Title = "Потушить пожар",
                            AllowedCrews = new[]
                            {
                                CrewType.Fire
                            },
                            Deadline = 75,
                            WorkTime = 8
                        },
                        new IncidentStage
                        {
                            Title = "Восстановить электропитание клуба",
                            AllowedCrews = new[]
                            {
                                CrewType.Repair
                            },
                            Deadline = 50,
                            WorkTime = 10
                        }
                    };
                }

                if (item.Id == "1.A")
                {
                    item.OnFailureIncidentId = "3.A";
                }

                if (item.Id == "3.A" && item.Stages.Length == 0)
                {
                    item.Stages = new[]
                    {
                        new IncidentStage
                        {
                            Title = "Локализовать возгорание щита",
                            AllowedCrews = new[]
                            {
                                CrewType.Fire
                            },
                            Deadline = 60,
                            WorkTime = 8
                        },
                        new IncidentStage
                        {
                            Title = "Восстановить подстанцию",
                            AllowedCrews = new[]
                            {
                                CrewType.Repair
                            },
                            Deadline = 50,
                            WorkTime = 12
                        }
                    };
                }

                EditorUtility.SetDirty(item);
            }

            Directory.CreateDirectory("Assets/Settings/Upgrades");
            var ids = new[]
            {
                "UP_OPS",
                "UP_FIRE",
                "UP_REPAIR",
                "UP_VEHICLE",
                "UP_RADIO",
                "UP_FOAM"
            };
            var labels = new[]
            {
                "Вторая опергруппа",
                "Вторые пожарные",
                "Вторая ремонтная бригада",
                "Автопарк",
                "Связь",
                "Пожарное оснащение"
            };
            var descriptions = new[]
            {
                "Отдельный экипаж и штекер; параллельный вызов.",
                "Отдельный экипаж и штекер; параллельное тушение.",
                "Отдельный экипаж и штекер; параллельный ремонт.",
                "Время пути всех служб уменьшается на 20%.",
                "Ранний сигнал для отмеченных ЧП: на 15 секунд раньше.",
                "Работа пожарных быстрее на 25%."
            };
            var costs = new[]
            {
                3,
                3,
                3,
                2,
                2,
                1
            };
            var content = new UpgradeDefinition[6];
            for (int i = 0; i < 6; i++)
            {
                string path = "Assets/Settings/Upgrades/" + ids[i] + ".asset";
                var item = AssetDatabase.LoadAssetAtPath<UpgradeDefinition>(path);
                if (item == null)
                {
                    item = ScriptableObject.CreateInstance<UpgradeDefinition>();
                    item.Id = ids[i];
                    item.Label = labels[i];
                    item.Description = descriptions[i];
                    item.Cost = costs[i];
                    item.Effect = (UpgradeEffect)i;
                    item.Multiplier = i == 3 ? .8f : i == 5 ? .75f : 1;
                    AssetDatabase.CreateAsset(item, path);
                }

                content[i] = item;
            }

            var rig = UnityEngine.Object.FindFirstObjectByType<Zvezda57.CameraSystem.CameraRig>();
            var serialized = new SerializedObject(rig);
            var points = serialized.FindProperty("focusPoints");
            for (int i = 0; i < points.arraySize; i++)
            {
                var point = points.GetArrayElementAtIndex(i);
                if (point.FindPropertyRelative("Mode").enumValueIndex == (int)Zvezda57.CameraSystem.CameraMode.Map)
                {
                    var position = point.FindPropertyRelative("Position");
                    if (Mathf.Abs(position.vector3Value.z - 1.2f) < .01f)
                    {
                        position.vector3Value = new Vector3(-2.8f, 2.1f, .8f);
                    }
                }
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
            scope.ConfigureUpgrades(content);
            EditorUtility.SetDirty(scope);
        }
    }
}
