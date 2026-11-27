using UnityEngine;
using Zvezda57.CameraSystem;
using Zvezda57.Documents;
using Zvezda57.InputSystem;
using Zvezda57.UI;
using Zvezda57.Visitors;

namespace Zvezda57.Systems
{
    internal sealed class DeskDevices : MonoBehaviour
    {
        [SerializeField]
        private WorldAction approveStamp, recheckStamp, detainStamp, inkPad, reference, magnifier, ultraviolet, governmentPhone;
        [SerializeField]
        private IncomingPhoneIndicator phoneIndicator;

        public void Bind(IDeskToolActions actions, ICampaignPlayState campaign)
        {
            BindStamp(approveStamp, CaseVerdict.Approve, actions);
            BindStamp(recheckStamp, CaseVerdict.Recheck, actions);
            BindStamp(detainStamp, CaseVerdict.Detain, actions);
            inkPad.Configure("Подушка", actions.InkStamp, CameraMode.Desk);
            reference.Configure("Справочник", actions.OpenReference, CameraMode.Desk);
            magnifier.Configure("Лупа", () => actions.SelectTool(InspectionTool.Magnifier), CameraMode.Desk);
            ultraviolet.Configure("УФ-лампа", () => actions.SelectTool(InspectionTool.Ultraviolet), CameraMode.Desk);
            governmentPhone.Configure("Вертушка", actions.AnswerCall, CameraMode.Desk);
            phoneIndicator.Configure(campaign);
        }

        private static void BindStamp(WorldAction stamp, CaseVerdict verdict, IDeskToolActions actions)
        {
            stamp.Configure("Взять штамп: " + InvestigationHud.VerdictName(verdict),
                () => actions.SelectStamp(verdict), CameraMode.Desk);
        }
    }
}
