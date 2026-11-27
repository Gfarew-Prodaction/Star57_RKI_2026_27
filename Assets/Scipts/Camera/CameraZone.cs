using UnityEngine;
using Zvezda57.InputSystem;

namespace Zvezda57.CameraSystem
{
    public sealed class CameraZone : MonoBehaviour
    {
        public CameraMode Mode;
        public string Caption;
        public void Bind(CameraRig rig)
        {
            GetComponent<WorldAction>().Configure(Caption, () => rig.Go(Mode), CameraMode.Overview);
        }
    }
}
