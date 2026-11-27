using Zvezda57.CameraSystem;

namespace Zvezda57.InputSystem
{
    public interface IWorldAction
    {
        string Hint { get; }

        bool CanUse(CameraMode mode);
        void Use();
        void Hover(bool active);
    }
}
