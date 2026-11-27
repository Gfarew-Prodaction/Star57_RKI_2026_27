using System;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Zvezda57.Dispatch;
using Zvezda57.Documents;
using Zvezda57.Systems;

namespace Zvezda57.UI
{
    public sealed class InvestigationHud : MonoBehaviour
    {
        [Serializable]
        private sealed class MessagePanel
        {
            public GameObject Root;
            public TMP_Text Body;
            public Button Close;
        }

        [SerializeField]
        private GameObject root, referencePanel, phonePanel, confirmation;
        [SerializeField]
        private Image probe;
        [SerializeField]
        private TMP_Text alert, tool, scan, dial, answer, referencePage, verdictText;
        [SerializeField]
        private LabeledButton[] referenceTabs;
        [SerializeField]
        private Button[] digits;
        [SerializeField]
        private Button journalButton, dialButton, clearButton, hangUpButton, confirmButton, changeVerdictButton;
        [SerializeField]
        private MessagePanel journalPanel, receiptPanel;
        [SerializeField]
        private Image receiptPortrait;
        [SerializeField]
        private MessagePanel[] sectorPanels;

        private GameObject _popup;
        private DeskHud _desk;
        private Func<string, string> _call;
        private Action _submit;
        private Action _cancel;
        private string _digits = "";
        private int _tab;
        internal int ReferenceCapacity => referenceTabs?.Length ?? 0;
        public bool ReferenceOpen => referencePanel.activeSelf;
        public int ReferenceTab => _tab;
        public string PhoneDigits => _digits;
        public string PhoneAnswer => answer.text;
        public bool IsBlocking => confirmation.activeSelf;

        public void Build(DeskHud hud, ReferenceDefinition content, Func<string, string> call, Action journal)
        {
            _desk = hud;
            _call = call;
            for (int i = 0; i < referenceTabs.Length; i++)
            {
                int tab = i;
                var button = referenceTabs[i];
                bool available = i < content.Tabs.Length;
                button.Show(available);
                if (available)
                {
                    button.SetCaption(content.Tabs[i]);
                    button.Bind(() => SelectTab(content, tab));
                }
            }

            for (int i = 0; i < digits.Length; i++)
            {
                int digit = i;
                digits[i].onClick.AddListener(() => EnterDigit(digit));
            }

            journalButton.onClick.AddListener(() => journal());
            dialButton.onClick.AddListener(() => answer.text = _digits.Length == 3 ? _call(_digits) : "Наберите три цифры.");
            clearButton.onClick.AddListener(ClearNumber);
            hangUpButton.onClick.AddListener(ClosePopup);
            confirmButton.onClick.AddListener(() => FinishConfirmation(_submit));
            changeVerdictButton.onClick.AddListener(() => FinishConfirmation(_cancel));
            BindMessage(journalPanel);
            BindMessage(receiptPanel);
            foreach (var panel in sectorPanels)
            {
                BindMessage(panel);
            }

            referencePanel.SetActive(false);
            phonePanel.SetActive(false);
            confirmation.SetActive(false);
            probe.gameObject.SetActive(false);
            root.SetActive(false);
        }

        public void Show()
        {
            root.SetActive(true);
        }

        private void BindMessage(MessagePanel panel)
        {
            panel.Root.SetActive(false);
            panel.Close.onClick.AddListener(ClosePopup);
        }

        private void SelectTab(ReferenceDefinition content, int tab)
        {
            _tab = tab;
            referencePage.text = content.Pages[tab];
        }

        public void RestoreReference(ReferenceDefinition content, bool open, int selected)
        {
            referencePanel.SetActive(open);
            if (open)
            {
                SelectTab(content, Mathf.Clamp(selected, 0, content.Pages.Length - 1));
            }
        }

        public void RestorePhone(string value, string result)
        {
            _digits = value ?? "";
            dial.text = "Телефон: " + _digits;
            if (!string.IsNullOrEmpty(result))
            {
                answer.text = result;
            }
        }

        public void ShowProbe(Vector2 point, InspectionTool selectedTool)
        {
            probe.gameObject.SetActive(true);
            var area = _desk.DocumentRect;
            var rect = area.rect;
            probe.rectTransform.position = area.TransformPoint(new Vector3(rect.xMin + point.x * rect.width, rect.yMin + point.y * rect.height, 0));
            probe.color = selectedTool == InspectionTool.Ultraviolet ? new Color(.45f, .15f, .9f, .2f) : new Color(1, .9f, .4f, .2f);
        }

        public void ToolStatus(string text)
        {
            tool.text = text;
        }

        public void ScanResult(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                probe.gameObject.SetActive(false);
            }

            scan.text = text;
            scan.transform.SetAsLastSibling();
        }

        public void Alerts(IDispatchStatus dispatch)
        {
            var active = dispatch.Incidents.Where(x => x.IsActive).ToArray();
            var urgent = active.OrderBy(x => x.Remaining).FirstOrDefault();
            alert.text = urgent == null ? "" : "Вызовы: " + active.Length + " · С-" + urgent.Sector + " · " + Mathf.CeilToInt(urgent.Remaining) + " с";
            alert.gameObject.SetActive(urgent != null);
        }

        public void ShowReference(ReferenceDefinition content)
        {
            referencePanel.SetActive(!ReferenceOpen);
            if (ReferenceOpen)
            {
                SelectTab(content, _tab);
            }
        }

        public void Phone()
        {
            OpenPopup(phonePanel);
            ClearNumber();
            answer.text = "2-0-4 архив · 3-1-5 КПП · 5-0-1 склад";
        }

        private void EnterDigit(int digit)
        {
            if (_digits.Length < 3)
            {
                _digits += digit;
            }

            dial.text = "Телефон: " + _digits;
        }

        private void ClearNumber()
        {
            _digits = "";
            dial.text = "Телефон: _ _ _";
        }

        public void ShowSector(int sector, string text)
        {
            ShowMessage(sectorPanels[sector - 1], $"СЕКТОР {sector}", text);
        }

        public void ShowJournal(string text)
        {
            ShowMessage(journalPanel, "ЖУРНАЛ РЕШЕНИЙ", text);
        }

        public void ShowReceipt(string text, Sprite expression)
        {
            if (receiptPortrait != null)
            {
                receiptPortrait.sprite = expression;
                receiptPortrait.gameObject.SetActive(expression != null);
            }

            ShowMessage(receiptPanel, "ДЕЛО СДАНО", text);
        }

        private void ShowMessage(MessagePanel panel, string title, string text)
        {
            OpenPopup(panel.Root);
            panel.Body.text = title + "\n\n" + text;
        }

        private void OpenPopup(GameObject panel)
        {
            ClosePopup();
            _popup = panel;
            panel.SetActive(true);
        }

        public void Confirm(CaseVerdict verdict, Action submit, Action cancel)
        {
            _submit = submit;
            _cancel = cancel;
            verdictText.text = "Сдать дело?\nРешение: " + VerdictName(verdict) + "\nПосле подтверждения решение попадёт в журнал.";
            confirmation.SetActive(true);
        }

        private void FinishConfirmation(Action action)
        {
            HideConfirmation();
            action?.Invoke();
        }

        private void HideConfirmation()
        {
            confirmation.SetActive(false);
            _submit = null;
            _cancel = null;
        }

        public void ClosePopup()
        {
            if (_popup != null)
            {
                _popup.SetActive(false);
                _popup = null;
            }
        }

        public void CloseAll()
        {
            ClosePopup();
            HideConfirmation();
            ScanResult("");
        }

        public static string VerdictName(CaseVerdict verdict) => verdict == CaseVerdict.Approve ? "РАЗРЕШИТЬ" : verdict == CaseVerdict.Recheck ? "НА ПРОВЕРКУ" : "ЗАДЕРЖАТЬ";
    }
}
