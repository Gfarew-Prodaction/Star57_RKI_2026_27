using System;
using UnityEngine;
using Zvezda57.CameraSystem;

namespace Zvezda57.Core
{
    [Serializable]
    public sealed class DeskSnapshot
    {
        public string CaseId;
        public bool Opened, ReferenceOpen;
        public int Page, ReferenceTab;
        public float Zoom = 26;
        public CameraMode Camera;
        public Vector3[] PaperPositions;
        public string PhoneDigits = "", PhoneAnswer = "";
    }
}
