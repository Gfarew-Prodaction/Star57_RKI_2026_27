using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Zvezda57.Core;
using Zvezda57.Documents;
using Zvezda57.Systems;

namespace Zvezda57.Editor
{
    public static class CampaignContentBuilder
    {
        private static T Asset<T>(string path, Action<T> initialize)
            where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null)
            {
                return asset;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(path));
            asset = ScriptableObject.CreateInstance<T>();
            initialize(asset);
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        private static DialogueChoice Choice(string text, int trust = 0, string flag = null, int minimum = -100) => new DialogueChoice
        {
            Text = text,
            TrustDelta = trust,
            SetFlag = flag,
            MinimumTrust = minimum
        };
        private static DialogueDefinition Dialogue(string id, string speaker, string text, params DialogueChoice[] choices) => Asset<DialogueDefinition>("Assets/Settings/Dialogue/" + id + ".asset", x =>
        {
            x.Id = id;
            x.Speaker = speaker;
            x.Text = text;
            x.Choices = choices;
        });
        public static void Configure(OfficeLifetimeScope scope)
        {
            var tutorial = Asset<CaseDefinition>("Assets/Settings/Cases/Case_0.1.asset", x =>
            {
                x.Id = "0.1";
                x.VisitorName = "Фомин · учебная папка через Юру";
                x.DeliveredByYura = true;
                x.Greeting = "Юра оставил учебную папку на столе.";
                x.Documents = new[]
                {
                    new DocumentContent
                    {
                        Title = "УЧЕБНОЕ ДЕЛО 0.1",
                        Body = "Фомин\nКБ-88 → Энергоблок\nПредписание от 13 октября 1957\nРеквизиты учреждения и срок исправны.\n\nПорядок проверки: прочесть лист, взять штамп, коснуться подушки, поставить оттиск и подтвердить сдачу."
                    },
                    new DocumentContent
                    {
                        Title = "ПАМЯТКА ДЕЖУРНОМУ",
                        Body = "ЛКМ — действие. Удержание — перенос бумаги.\nНазад — общий ракурс.\nНа карте: выбрать штекер, затем сектор.\nРемонтники устраняют неисправность в секторе 2.\n\nСправочник — на столе.\nОбязательные действия доступны мышью."
                    }
                };
                x.Verdicts = Enumerable.Range(0, 3).Select(i => new VerdictRule { Verdict = (CaseVerdict)i, Accepted = i == 0, Feedback = i == 0 ? "Учебное дело проверено верно." : "Реквизиты исправны: требовалось разрешение.", Metrics = new MetricDelta(i == 0 ? 2 : -2, 0, 0) }).ToArray();
            });
            var intro0 = Dialogue("D0_INTRO", "Юра Соловьёв", "Товарищ майор, я стажёр Юра. Покажу кабинет. Учебная папка уже на столе: КБ-88, энергоблок. Получите её, прочтите документы, затем штамп → подушка → лист → сдача. На карте ждёт вызов ремонтников.", Choice("Спасибо, Юра. Разберёмся вместе.", 1), Choice("Приступайте к работе."));
            var intro1 = Dialogue("D1_INTRO", "Юра", "Ветров из НИИ-42 приехал раньше срока. Я знаю его через Аню, но документы всё равно надо проверить. Сегодня также ждём груз Семёнова.", Choice("Проверим основания допуска."));
            var intro2 = Dialogue("D2_ANYA", "Юра", "Аня выносила испорченный прибор и боялась выговора. Прошу разобраться, прежде чем арестовывать. После неё придёт Морозов. Теперь пригодятся телефон, лупа и УФ.", Choice("Проверю факты и выслушаю её."), Choice("Решение приму по документам."));
            var intro3 = Dialogue("D3_INTRO", "Юра", "Сегодня пуск. Лаврентьев принесёт спецпредписание, а курьер Сомов — накладную с радиограммой. Проверьте бумагу лупой и УФ. Если доверия не хватит для радиоигры, поручите мне проследить за курьером.", Choice("Начинаем последнюю смену."));
            var phone = Dialogue("D0_PHONE", "Генерал Руденко · правительственный телефон", "В городе действует агент «Кварц». До пуска осталось три смены. Документы, вызовы и готовность реактора связаны. Не теряйте секретные материалы. Завершайте смену после работы с делами и вызовами.", Choice("Задачу понял.", 0, "Briefed"));
            var radio = Dialogue("D3_YURA", "Юра", "Сколотая «к» — с моей машинки. Ею пользовался Ветров. Скрытая схема подтверждает канал связи. Если доверяете мне, проведём радиоигру и выведем его к вокзалу. Иначе нужен курьерский след.", Choice("Доверяю. Готовь радиоигру.", 0, "RadioGame", 2), Choice("Будем работать по курьерскому следу."));
            var archive = Dialogue("YURA_ARCHIVE", "Юра · результат поручения", "Архивная сверка завершена. Обратите внимание на сроки прибытия и подлинность приказов. Личность Морозова всё равно нужно проверить по телефону 204 — факт по конкретному делу не заменяется общим поручением.", Choice("Принято."));
            var courier = Dialogue("YURA_COURIER", "Юра · результат поручения", "Курьерский маршрут установлен: материалы идут к вокзалу. Теперь опергруппа может провести перехват без радиоигры. На последнем вызове направьте её в сектор 1 до ухода поезда.", Choice("Передаю ориентировку опергруппе.", 0, "CourierTracked"));
            string[][] caseIds =
            {
                new[]
                {
                    "0.1"
                },
                new[]
                {
                    "1.2",
                    "1.3"
                },
                new[]
                {
                    "2.2",
                    "2.3"
                },
                new[]
                {
                    "3.1",
                    "3.2"
                }
            };
            string[][] incidentIds =
            {
                new[]
                {
                    "0.A"
                },
                new[]
                {
                    "1.A",
                    "1.B"
                },
                new[]
                {
                    "2.A",
                    "2.B",
                    "2.C"
                },
                new[]
                {
                    "3.A",
                    "3.B"
                }
            };
            var intros = new[]
            {
                intro0,
                intro1,
                intro2,
                intro3
            };
            var shifts = new ShiftDefinition[4];
            for (int i = 0; i < 4; i++)
            {
                int day = i;
                shifts[i] = Asset<ShiftDefinition>("Assets/Settings/Shifts/Shift_" + i + ".asset", x =>
                {
                    x.Title = (13 + day) + " октября 1957 · " + (day == 0 ? "Обучение" : "Смена " + day);
                    x.CaseIds = caseIds[day];
                    x.Intro = intros[day];
                    x.Reward = day == 1 ? 3 : day == 2 ? 2 : 0;
                    x.Incidents = incidentIds[day].Select((id, index) => new ScheduledIncident { Id = id, Delay = index == 0 ? 3 : day == 2 && index == 1 ? 3 : 15 + index * 10 }).ToArray();
                });
            }

            var endings = new[]
            {
                Asset<EndingDefinition>("Assets/Settings/Endings/E_GOOD.asset", x =>
                {
                    x.Kind = EndingKind.Good;
                    x.Title = "Пуск и перехват";
                    x.Text = "Ветров задержан, секретные материалы сохранены. Первый контур «Токамака-1» вышел на расчётный режим. Город встречает утро без тревоги.";
                }),
                Asset<EndingDefinition>("Assets/Settings/Endings/E_MIXED.asset", x =>
                {
                    x.Kind = EndingKind.Mixed;
                    x.Title = "Перехват со срывом пуска";
                    x.Text = "Агент задержан, но готовности реактора недостаточно. Пуск отложен. Диверсия раскрыта ценой сорванного срока.";
                }),
                Asset<EndingDefinition>("Assets/Settings/Endings/E_BAD.asset", x =>
                {
                    x.Kind = EndingKind.Bad;
                    x.Title = "Утечка или катастрофа";
                    x.Text = "Перехват не обеспечил сохранность материалов либо критический показатель города исчерпан. Агентский канал не удалось надёжно перекрыть. В городе звучит тревога.";
                })
            };
            var campaign = Asset<CampaignDefinition>("Assets/Settings/Shifts/Campaign.asset", x =>
            {
                x.Shifts = shifts;
                x.Story = new[]
                {
                    phone,
                    radio,
                    archive,
                    courier
                };
                x.Endings = endings;
                x.ReactorThreshold = 60;
                x.YuraTaskSeconds = 8;
            });
            if (campaign.RadioSteps == null || campaign.RadioSteps.Length == 0)
            {
                campaign.RadioSteps = new[]
                {
                    new RadioStep
                    {
                        Prompt = "На скрытой схеме Сомова указан канал: РАДИОУЗЕЛ. Выберите пункт связи, чтобы вызвать нужного адресата.",
                        Options = new[]
                        {
                            "Канал: жилой квартал",
                            "Канал: радиоузел",
                            "Канал: энергоблок"
                        },
                        CorrectOption = 1
                    },
                    new RadioStep
                    {
                        Prompt = "Входящий запрос: «Контрольная буква?» Дефект машинки — сколотая «к». Передайте код, сохранив примету отправителя.",
                        Options = new[]
                        {
                            "Контроль: ноль",
                            "Контроль: О",
                            "Контроль: к"
                        },
                        CorrectOption = 2
                    },
                    new RadioStep
                    {
                        Prompt = "Адресат принял код. Курьерский маршрут идёт к вокзалу. Направьте агента к месту, где его сможет перехватить опергруппа.",
                        Options = new[]
                        {
                            "Встреча: сектор 1, вокзал",
                            "Встреча: сектор 4, подстанция",
                            "Встреча: сектор 2, клуб"
                        },
                        CorrectOption = 0
                    }
                };
                campaign.RadioSeconds = 35;
                campaign.IncomingCallSeconds = 10;
                EditorUtility.SetDirty(campaign);
            }

            scope.ConfigureCampaign(campaign);
            var cases = AssetDatabase.FindAssets("t:CaseDefinition", new[] { "Assets/Settings/Cases" }).Select(g => AssetDatabase.LoadAssetAtPath<CaseDefinition>(AssetDatabase.GUIDToAssetPath(g))).OrderBy(x => x.Id).ToArray();
            scope.ConfigureCases(cases);
            EditorUtility.SetDirty(scope);
        }

        [MenuItem("Zvezda57/Upgrade campaign")]
        public static void Upgrade()
        {
            var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(Zvezda57.Core.SceneNavigation.GamePath);
            Configure(UnityEngine.Object.FindFirstObjectByType<OfficeLifetimeScope>());
            AssetDatabase.SaveAssets();
            UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
        }
    }
}
