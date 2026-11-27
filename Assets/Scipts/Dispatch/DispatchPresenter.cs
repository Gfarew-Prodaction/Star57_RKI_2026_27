using System;
using System.Linq;
using UnityEngine;
using Zvezda57.CameraSystem;
using Zvezda57.InputSystem;

namespace Zvezda57.Dispatch
{
    public sealed class DispatchPresenter : MonoBehaviour
    {
        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        private static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");
        [SerializeField]
        private Color idleColor = new Color(.15f, .3f, .23f), waitingColor = new Color(.7f, .2f, .13f), assignedColor = new Color(.6f, .48f, .1f);
        [SerializeField]
        private Color failedColor = new Color(.7f, .18f, .51f);
        private IDispatchService _service;
        private Action<string> _feedback;
        private Action<int, string> _details;
        private int? _selected;
        public bool IsSelected(CrewType type) => _selected is int index && _service.Crews[index].Type == type;

        internal Transform SectorTarget(int number) => Array.Find(sectors, socket => socket.Sector == number)?.transform;

        [SerializeField]
        private SectorSocket[] sectors;
        [SerializeField]
        private CrewPlug[] crewSlots;

        internal int CrewCapacity => crewSlots?.Length ?? 0;

        private MaterialPropertyBlock _signalProperties;

        public void Build(IDispatchService service, Action<string> feedback, Action<int, string> details, CameraRig rig)
        {
            _service = service;
            _feedback = feedback;
            _details = details;
            _signalProperties = new MaterialPropertyBlock();

            foreach (var socket in sectors)
            {
                int sector = socket.Sector;
                socket.GetComponent<WorldAction>().Configure("Сектор " + sector + ": вызовы / назначить выбранную службу", () =>
                {
                    if (rig.Mode == CameraMode.Overview)
                    {
                        rig.Go(CameraMode.Map);
                    }
                    else if (rig.Mode == CameraMode.Map)
                    {
                        SelectSector(sector);
                    }
                });
            }

            for (int i = 0; i < crewSlots.Length; i++)
            {
                crewSlots[i].Configure(i, _service, Select, Drop);
            }

            service.Changed += Refresh;
            Refresh();
        }

        private void Select(int crewId)
        {
            var crew = _service.Crews[crewId];
            if (crew.State != CrewState.Free)
            {
                _feedback("Экипаж занят и не может быть отправлен повторно.");
                return;
            }

            if (_selected == crewId)
            {
                _selected = null;
                RefreshCrewLabels();
                _feedback("Выбор службы отменён.");
                return;
            }

            _selected = crewId;
            RefreshCrewLabels();
            _feedback("Выбрана служба: " + CrewName(crew.Type) + ". Кликните по сектору или перетащите штекер в гнездо.");
        }

        private void SelectSector(int sector)
        {
            if (_selected.HasValue)
            {
                Drop(_selected.Value, sector);
                return;
            }

            var incidents = _service.Incidents.Where(incident => incident.Sector == sector && incident.State != IncidentState.Inactive);
            string details = string.Join("\n\n", incidents.Select(DescribeIncident));
            _details(sector, details.Length == 0 ? "Вызовов нет." : details);
        }

        private static string DescribeIncident(IIncidentStatus incident)
        {
            string crews = string.Join(", ", incident.AllowedCrews.Select(CrewName));
            return $"{incident.Title}\n{incident.Description}\nЭтап {incident.StageIndex + 1}/{incident.StageCount}: {incident.StageTitle}\n" +
                $"{StateName(incident.State)} · {Mathf.CeilToInt(incident.Remaining)} с\nСлужбы: {crews}";
        }

        private void Drop(int crewId, int sector)
        {
            _service.DispatchToSector(crewId, sector, out string message);
            if (_service.Crews[crewId].State != CrewState.Free)
            {
                _selected = null;
                RefreshCrewLabels();
            }

            _feedback(message);
        }

        private void Refresh()
        {
            foreach (var sector in sectors)
            {
                var incidents = _service.Incidents.Where(incident => incident.Sector == sector.Sector);
                var active = incidents.Where(incident => incident.IsActive).ToArray();
                var incident = active.FirstOrDefault();
                bool failed = incidents.Any(item => item.State == IncidentState.Failed);
                string remaining = incident == null ? string.Empty : Mathf.CeilToInt(incident.Remaining) + " с";
                string idleCaption = failed ? "Провал" : string.Empty;
                string sectorName = "С-" + sector.Sector + (active.Length > 1 ? " [" + active.Length + "]" : "");
                if (sector.Indicator != null)
                {
                    sector.Label.text = incident == null ? idleCaption :
                        (active.Length > 1 ? active.Length + " · " : "") + remaining + (failed ? " · !" : "");
                }
                else
                {
                    sector.Label.text = sectorName + (incident == null
                        ? "\n" + (failed ? idleCaption : "—")
                        : "\n" + StateName(incident.State) + " " + (incident.StageIndex + 1) + "/" + incident.StageCount + "\n" + remaining + (failed ? " · !" : ""));
                }

                Color signal = incident == null
                    ? (failed ? failedColor : idleColor)
                    : (incident.State == IncidentState.Waiting ? waitingColor : assignedColor);
                _signalProperties.SetColor(BaseColor, signal);
                _signalProperties.SetColor(EmissionColor, signal * (incident != null || failed ? 1.65f : .18f));
                (sector.Indicator != null ? sector.Indicator : sector.GetComponent<Renderer>()).SetPropertyBlock(_signalProperties);
            }

            RefreshCrewLabels();
        }

        private void RefreshCrewLabels()
        {
            for (int i = 0; i < crewSlots.Length; i++)
            {
                var slot = crewSlots[i];
                bool available = i < _service.Crews.Count;
                slot.gameObject.SetActive(available);
                slot.Label.gameObject.SetActive(available);
                if (!available)
                {
                    continue;
                }

                var crew = _service.Crews[i];
                string status = crew.State == CrewState.Free && _selected == i
                    ? "Выбран"
                    : CrewStateName(crew.State);
                if (crew.State != CrewState.Free)
                {
                    status += " " + Mathf.CeilToInt(crew.Remaining);
                }

                slot.Label.text = CrewName(crew.Type) + "\n" + status;
            }
        }

        public static string CrewName(CrewType type) => new[]
        {
            "Опергруппа",
            "ВОХР",
            "Пожарные",
            "Ремонтники"
        }[(int)type];
        public static string CrewStateName(CrewState state) => new[]
        {
            "Свободен",
            "В пути",
            "Работа",
            "Возврат"
        }[(int)state];
        public static string StateName(IncidentState state) => new[]
        {
            "Не начат",
            "Ожидает",
            "Назначен",
            "Завершён",
            "Провален"
        }[(int)state];
        private void OnDestroy()
        {
            if (_service != null)
            {
                _service.Changed -= Refresh;
            }
        }
    }
}
