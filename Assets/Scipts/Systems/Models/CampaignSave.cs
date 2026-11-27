using System;
using System.Collections.Generic;
using Zvezda57.Core;

namespace Zvezda57.Systems
{
    [Serializable]
    public sealed class CampaignSave
    {
        public QueueSnapshot Queue;
        public ReviewSnapshot Review;
        public DispatchSnapshot Dispatch;
        public DeskSnapshot Desk;
        public CampaignPhase ResumePhase;
        public IncomingCallState CallState;
        public string CallDialogue;
        public float CallRemaining;
        public int RadioIndex, RadioMistakes;
        public float RadioRemaining;
        public CityMetricsState Metrics;
        public int Version = 1, ShiftIndex, YuraTrust;
        public CampaignPhase Phase = CampaignPhase.Menu;
        public List<string> Flags = new List<string>();
        public List<IncidentResult> Incidents = new List<IncidentResult>();
        public List<string> SeenDialogues = new List<string>();
        public string PendingDialogue;
        public YuraTask Task;
        public float TaskRemaining;
        public bool FinalTriggered;
        public EndingKind Ending;
    }
}
