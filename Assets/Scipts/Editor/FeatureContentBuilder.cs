using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using Zvezda57.Dispatch;
using Zvezda57.Documents;
using Zvezda57.Systems;

namespace Zvezda57.Editor
{
    public static class FeatureContentBuilder
    {
        public static void UpgradeCases(CaseDefinition[] cases)
        {
            foreach (var asset in cases)
            {
                if (asset.Verdicts.Length == 0)
                {
                    var expected = asset.Id == "1.3" || asset.Id == "2.3" ? CaseVerdict.Detain : asset.Id == "2.2" || asset.Id == "3.2" ? CaseVerdict.Recheck : CaseVerdict.Approve;
                    asset.Verdicts = new VerdictRule[3];
                    for (int i = 0; i < 3; i++)
                    {
                        var verdict = (CaseVerdict)i;
                        bool accepted = verdict == expected || (asset.Id == "1.2" && verdict == CaseVerdict.Recheck) || (asset.Id == "3.2" && verdict == CaseVerdict.Detain);
                        asset.Verdicts[i] = new VerdictRule
                        {
                            Verdict = verdict,
                            Accepted = accepted,
                            Feedback = (accepted ? "Решение принято. " : "Проверка выявила ошибку. ") + Explanation(asset.Id),
                            IncidentId = asset.Id == "1.3" && verdict == CaseVerdict.Approve ? "1.C" : ""
                        };
                    }
                }

                if (asset.PhoneResponses.Length == 0)
                {
                    string archive = asset.Id == "2.3" ? "Настоящий Морозов снят с поезда в Туле и находится в больнице. Портфель украден." : asset.Id == "1.2" ? "Приказ Ветрова подлинный. Прибытие запланировано на 20 октября; он выехал раньше срока." : "Архив подтверждает регистрацию документов. Дополнительных сведений нет.";
                    asset.PhoneResponses = new[]
                    {
                        new PhoneResponse
                        {
                            Number = "204",
                            EvidenceId = "archive_" + asset.Id,
                            Text = archive
                        },
                        new PhoneResponse
                        {
                            Number = "315",
                            EvidenceId = "checkpoint_" + asset.Id,
                            Text = "КПП: прибытие посетителя зарегистрировано. Сверьте цель поездки с предписанием."
                        },
                        new PhoneResponse
                        {
                            Number = "501",
                            EvidenceId = "depot_" + asset.Id,
                            Text = asset.Id == "1.3" ? "Склад: тяжёлую воду в жилой сектор не заказывали. ГР-04 направляется в лабораторию «Токамак». НИИ-12 — другой город." : "Склад: заявок по этому посетителю не зарегистрировано."
                        }
                    };
                }

                if (asset.Documents[0].Evidence.Length == 0)
                {
                    asset.Documents[0].Evidence = new[]
                    {
                        new EvidenceDefinition
                        {
                            Id = "magnifier_" + asset.Id,
                            Tool = InspectionTool.Magnifier,
                            Region = new Rect(0, .65f, 1, .25f),
                            Finding = asset.Id == "2.2" ? "Лупа: подпись Лаврентьева обведена карандашом." : asset.Id == "3.2" ? "Лупа: у литеры «к» сколот нижний штрих." : "Лупа: штрихи печати и реквизиты различимы; явных исправлений в этой области нет."
                        },
                        new EvidenceDefinition
                        {
                            Id = "uv_" + asset.Id,
                            Tool = InspectionTool.Ultraviolet,
                            Region = new Rect(.15f, .1f, .7f, .75f),
                            Finding = asset.Id == "3.2" ? "УФ: под текстом проступает скрытая схема бобин." : "УФ: защитная сетка «АТОМ-57» присутствует."
                        }
                    };
                }

                asset.Documents[1].Body = "Особый сектор\n\nПолучатель: майор Глеб Ковалёв\nПосетитель: " + asset.VisitorName + "\n\nПроверить документы и реквизиты.\nТелефоны: 204 — архив, 315 — КПП, 501 — склад.";
                EditorUtility.SetDirty(asset);
            }
        }

        private static string Explanation(string id)
        {
            switch (id)
            {
                case "1.2":
                    return "Приказ Ветрова подлинный, но срок прибытия требует внимания.";
                case "1.3":
                    return "Склад не заказывал тяжёлую воду в жилой сектор. Допуск груза создаёт риск на складе.";
                case "2.2":
                    return "Подпись обведена карандашом; нужны дополнительные проверки, оснований для ареста недостаточно.";
                case "2.3":
                    return "Архив сообщает о краже портфеля настоящего Морозова. Посетитель — самозванец.";
                case "3.1":
                    return "Спецпредписание Лаврентьева исправно, задержка мешает пуску.";
                default:
                    return "Сколотая литера и скрытая схема требуют проверки или задержания курьера.";
            }
        }

        public static IncidentDefinition[] Incidents()
        {
            Directory.CreateDirectory("Assets/Settings/Incidents");
            var ids = new[]
            {
                "0.A",
                "1.A",
                "1.B",
                "1.C",
                "2.A",
                "2.B",
                "2.C",
                "3.A",
                "3.B",
                "3.C"
            };
            var titles = new[]
            {
                "Пробки в Доме учёных",
                "Перегрузка фидера №2",
                "Растяжка у северной просеки",
                "Сорвана пломба на складе",
                "Пожар в клубе",
                "Фотограф на вокзале",
                "Нелегальный коротковолновый эфир",
                "Диверсия на подстанции",
                "Дымовая шашка у ворот",
                "Ветров пытается сесть на поезд"
            };
            var sectors = new[]
            {
                2,
                4,
                6,
                5,
                2,
                1,
                6,
                4,
                5,
                1
            };
            var crews = new[]
            {
                new[]
                {
                    CrewType.Repair
                },
                new[]
                {
                    CrewType.Repair
                },
                new[]
                {
                    CrewType.Guard,
                    CrewType.Ops
                },
                new[]
                {
                    CrewType.Ops
                },
                new[]
                {
                    CrewType.Fire
                },
                new[]
                {
                    CrewType.Guard,
                    CrewType.Ops
                },
                new[]
                {
                    CrewType.Ops
                },
                new[]
                {
                    CrewType.Repair
                },
                new[]
                {
                    CrewType.Guard
                },
                new[]
                {
                    CrewType.Ops
                }
            };
            var times = new[]
            {
                5f,
                30,
                65,
                0,
                95,
                95,
                140,
                185,
                215,
                245
            };
            var result = new IncidentDefinition[ids.Length];
            for (int i = 0; i < ids.Length; i++)
            {
                var path = "Assets/Settings/Incidents/Incident_" + ids[i] + ".asset";
                var asset = AssetDatabase.LoadAssetAtPath<IncidentDefinition>(path);
                if (asset == null)
                {
                    asset = ScriptableObject.CreateInstance<IncidentDefinition>();
                    asset.Id = ids[i];
                    asset.Title = titles[i];
                    asset.Description = titles[i] + ". Направьте подходящую службу до истечения срока прибытия.";
                    asset.Sector = sectors[i];
                    asset.AllowedCrews = crews[i];
                    asset.AutoStart = i != 3;
                    asset.StartAfter = times[i];
                    asset.Deadline = i == 9 ? 40 : 75;
                    asset.TravelTime = 4;
                    asset.WorkTime = 8;
                    asset.ReturnTime = 4;
                    AssetDatabase.CreateAsset(asset, path);
                }

                result[i] = asset;
            }

            return result;
        }

        public static ReferenceDefinition Reference()
        {
            Directory.CreateDirectory("Assets/Settings/Reference");
            const string path = "Assets/Settings/Reference/Reference.asset";
            var asset = AssetDatabase.LoadAssetAtPath<ReferenceDefinition>(path);
            if (asset != null)
            {
                return asset;
            }

            asset = ScriptableObject.CreateInstance<ReferenceDefinition>();
            asset.Tabs = new[]
            {
                "Учреждения",
                "Грузы",
                "Телефоны",
                "Защита"
            };
            asset.Pages = new[]
            {
                "УЧРЕЖДЕНИЯ\n\nНИИ-42 — акустическая лаборатория.\nКБ-88 — энергоблок.\nНИИ-12 — учреждение другого города.\n\nСверяйте место работы, назначение и срок прибытия с предписанием.",
                "МАРШРУТЫ ГРУЗОВ\n\nГР-01 — обычное снабжение.\nГР-04 — тяжёлая вода, лаборатория «Токамак», сектор 3.\nГР-09 — оборудование по спецпредписанию.\n\nЖилой сектор не принимает тяжёлую воду.",
                "СЛУЖЕБНЫЕ ТЕЛЕФОНЫ\n\n204 — архив\n315 — КПП\n501 — склад\n\nОтвет относится к текущей полученной папке. Звонок записывает подтверждённые факты.",
                "ПРОВЕРКА БУМАГ\n\nЛупа: ведите по строкам документа, сверяйте штрихи и подпись.\n\nУФ: защитная сетка «АТОМ-57». Подлинная сетка сама по себе не удостоверяет личность.\n\nПрототип: приборы доступны сразу."
            };
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        [MenuItem("Zvezda57/Upgrade investigation prototype")]
        public static void UpgradeScene()
        {
            var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(Zvezda57.Core.SceneNavigation.GamePath);
            var scope = UnityEngine.Object.FindFirstObjectByType<Zvezda57.Core.OfficeLifetimeScope>();
            var paths = AssetDatabase.FindAssets("t:CaseDefinition", new[] { "Assets/Settings/Cases" });
            var cases = Array.ConvertAll(paths, guid => AssetDatabase.LoadAssetAtPath<CaseDefinition>(AssetDatabase.GUIDToAssetPath(guid)));
            UpgradeCases(cases);
            var incidents = Incidents();
            scope.ConfigureFeatures(incidents, Reference());
            OperationsContentBuilder.Configure(scope, cases, incidents);
            CampaignContentBuilder.Configure(scope);
            EditorUtility.SetDirty(scope);
            UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
        }
    }
}
