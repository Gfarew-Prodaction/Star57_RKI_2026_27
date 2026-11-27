using System;
using UnityEngine;

namespace Zvezda57.CameraSystem
{
    [Serializable]
    public struct CameraFocus
    {
        public CameraMode Mode;
        public Vector3 Position;
        public Vector3 Euler;
    }
}
