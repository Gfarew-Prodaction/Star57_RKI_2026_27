using UnityEngine;

namespace Zvezda57.InputSystem
{
    public interface IWorldDrag
    {
        void Begin(Ray ray);
        void Drag(Ray ray);
        void End(Ray ray, bool cancel);
    }
}
