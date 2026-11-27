using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Zvezda57.Systems;

namespace Zvezda57.UI
{
    public sealed class OperationsHud : MonoBehaviour
    {
        [Serializable]
        private sealed class UpgradeRow
        {
            public TMP_Text Label;
            public Button Toggle;
        }

        [SerializeField]
        private Transform root;
        [SerializeField]
        private GameObject upgradesPanel, historyPanel;
        [SerializeField]
        private TMP_Text values, summary, history;
        [SerializeField]
        private Button openUpgradesButton, openHistoryButton, closeUpgradesButton, closeHistoryButton, applyButton, clearButton;
        [SerializeField]
        private UpgradeRow[] upgradeRows;

        private ICityMetrics _metrics;
        private IUpgradeService _upgrades;
        private string _message = "";

        internal int UpgradeCapacity => upgradeRows?.Length ?? 0;

        public void Configure(ICityMetrics metrics, IUpgradeService upgrades)
        {
            _metrics = metrics;
            _upgrades = upgrades;
            openUpgradesButton.onClick.AddListener(OpenUpgrades);
            openHistoryButton.onClick.AddListener(History);
            closeUpgradesButton.onClick.AddListener(Close);
            closeHistoryButton.onClick.AddListener(Close);
            applyButton.onClick.AddListener(Commit);
            clearButton.onClick.AddListener(ClearSelection);

            for (int i = 0; i < upgradeRows.Length; i++)
            {
                bool available = i < _upgrades.Definitions.Count;
                var row = upgradeRows[i];
                row.Toggle.gameObject.SetActive(available);
                row.Label.gameObject.SetActive(available);
                if (available)
                {
                    string id = _upgrades.Definitions[i].Id;
                    row.Toggle.onClick.AddListener(() => Toggle(id));
                }
            }

            _metrics.Changed += Refresh;
            _upgrades.Changed += Refresh;
            upgradesPanel.SetActive(false);
            historyPanel.SetActive(false);
            root.gameObject.SetActive(false);
            Refresh();
        }

        public void Show()
        {
            root.gameObject.SetActive(true);
        }

        private void Refresh()
        {
            values.text = $"Реактор {_metrics.Reactor} · Секретность {_metrics.Secrecy} · Спокойствие {_metrics.Calm}";
            if (!upgradesPanel.activeSelf)
            {
                return;
            }

            summary.text = $"Резерв {_upgrades.Points} · В ведомости {_upgrades.DraftCost}\n{_message}";
            for (int i = 0; i < Mathf.Min(upgradeRows.Length, _upgrades.Definitions.Count); i++)
            {
                var item = _upgrades.Definitions[i];
                string state = _upgrades.Owns(item.Id) ? "[КУПЛЕНО] " : _upgrades.IsSelected(item.Id) ? "[ВЫБРАНО] " : "";
                upgradeRows[i].Label.text = $"{state}{item.Label} · {item.Cost} очк.\n{item.Description}";
            }
        }

        public void OpenUpgrades()
        {
            Close();
            root.SetAsLastSibling();
            upgradesPanel.SetActive(true);
            Refresh();
        }

        private void Toggle(string id)
        {
            _upgrades.Toggle(id, out _message);
            Refresh();
        }

        private void Commit()
        {
            try
            {
                _upgrades.Commit(out _message);
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
            {
                _message = "Сохранение не удалось: " + exception.Message;
            }

            Refresh();
        }

        private void ClearSelection()
        {
            _upgrades.Cancel();
            _message = "Выбор сброшен.";
            Refresh();
        }

        public void History()
        {
            Close();
            history.text = _metrics.History.Count == 0 ? "Изменений пока нет." : string.Join("\n\n", _metrics.History.Reverse().Take(7));
            historyPanel.SetActive(true);
        }

        public void Close()
        {
            upgradesPanel.SetActive(false);
            historyPanel.SetActive(false);
            _upgrades?.Cancel();
        }

        private void OnDestroy()
        {
            if (_metrics != null)
            {
                _metrics.Changed -= Refresh;
            }

            if (_upgrades != null)
            {
                _upgrades.Changed -= Refresh;
            }
        }
    }
}
