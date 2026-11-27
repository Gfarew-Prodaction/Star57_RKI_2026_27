using System;
using System.Collections.Generic;
using System.Linq;
using Zvezda57.Core;
using Zvezda57.Dispatch;

namespace Zvezda57.Systems
{
    public sealed class UpgradeService : IUpgradeService
    {
        private readonly IDispatchService _dispatch;
        private readonly JsonSaveFile<UpgradeSave> _file;
        private UpgradeSave _save;
        private readonly HashSet<string> _draft = new HashSet<string>();
        public IReadOnlyList<UpgradeDefinition> Definitions { get; }
        public bool PurchasingAllowed { get; set; } = true;
        public int Points => _save.Points;
        public int DraftCost => Definitions.Where(x => _draft.Contains(x.Id)).Sum(x => x.Cost);

        public event Action Changed;
        public UpgradeService(UpgradeDefinition[] definitions, IDispatchService dispatch, string path)
        {
            Definitions = Array.AsReadOnly(definitions);
            _dispatch = dispatch;
            _file = new JsonSaveFile<UpgradeSave>(path, "upgrade progress");
            _save = _file.Load(saved => saved.Purchased != null && saved.Rewards != null && saved.Points >= 0)
                ?? new UpgradeSave();

            foreach (var id in _save.Purchased)
            {
                var definition = Definitions.FirstOrDefault(x => x.Id == id);
                if (definition != null)
                {
                    Apply(definition);
                }
            }
        }

        public bool Owns(string id) => _save.Purchased.Contains(id);
        public bool IsSelected(string id) => _draft.Contains(id);

        public bool Toggle(string id, out string message)
        {
            if (!PurchasingAllowed)
            {
                message = "Улучшения доступны в сводке между сменами.";
                return false;
            }

            var definition = Definitions.FirstOrDefault(x => x.Id == id);
            if (definition == null || Owns(id))
            {
                message = "Улучшение недоступно или уже куплено.";
                return false;
            }

            if (_draft.Remove(id))
            {
                message = "Выбор отменён.";
                Changed?.Invoke();
                return true;
            }

            if (DraftCost + definition.Cost > Points)
            {
                message = "Недостаточно резервных очков.";
                return false;
            }

            _draft.Add(id);
            message = "Добавлено в ведомость; эффект после подтверждения.";
            Changed?.Invoke();
            return true;
        }

        public void Cancel()
        {
            _draft.Clear();
            Changed?.Invoke();
        }

        public bool Commit(out string message)
        {
            if (!PurchasingAllowed)
            {
                message = "Дождитесь сводки смены.";
                return false;
            }

            if (_draft.Count == 0 || DraftCost > Points)
            {
                message = "Ведомость пуста или превышает резерв.";
                return false;
            }

            var updated = Copy();
            updated.Points -= DraftCost;
            updated.Purchased.AddRange(Definitions.Where(x => _draft.Contains(x.Id)).Select(x => x.Id));
            Persist(updated);
            foreach (var definition in Definitions.Where(x => _draft.Contains(x.Id)))
            {
                Apply(definition);
            }

            _draft.Clear();
            message = "Улучшения подтверждены.";
            Changed?.Invoke();
            return true;
        }

        public void Reward(string token, int points)
        {
            if (points <= 0 || _save.Rewards.Contains(token))
            {
                return;
            }

            var updated = Copy();
            updated.Rewards.Add(token);
            updated.Points += points;
            Persist(updated);
            Changed?.Invoke();
        }

        private UpgradeSave Copy() => new UpgradeSave
        {
            Points = _save.Points,
            Purchased = new List<string>(_save.Purchased),
            Rewards = new List<string>(_save.Rewards)
        };
        private void Persist(UpgradeSave updated)
        {
            _file.Write(updated);
            _save = updated;
        }

        private void Apply(UpgradeDefinition definition)
        {
            switch (definition.Effect)
            {
                case UpgradeEffect.ExtraOps:
                    _dispatch.AddCrew(CrewType.Ops);
                    break;
                case UpgradeEffect.ExtraFire:
                    _dispatch.AddCrew(CrewType.Fire);
                    break;
                case UpgradeEffect.ExtraRepair:
                    _dispatch.AddCrew(CrewType.Repair);
                    break;
                case UpgradeEffect.Vehicle:
                    _dispatch.TravelMultiplier = definition.Multiplier;
                    break;
                case UpgradeEffect.Radio:
                    _dispatch.EarlyWarningSeconds = 15;
                    break;
                case UpgradeEffect.Foam:
                    _dispatch.FireWorkMultiplier = definition.Multiplier;
                    break;
            }
        }

        public void Clear()
        {
            _file.Delete();
        }
    }
}
