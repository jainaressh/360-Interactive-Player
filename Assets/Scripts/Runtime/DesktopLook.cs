using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR;

namespace Interactive360
{
    /// <summary>Editor / desktop fallback: hold the right mouse button and drag (or use arrow keys) to look around when no HMD is active.</summary>
    public sealed class DesktopLook : MonoBehaviour
    {
        public float mouseSensitivity = 0.15f;
        public float keySpeed = 90f;

        float yaw, pitch;
        bool initialised;

        void Update()
        {
            if (XRSettings.isDeviceActive) return;

            if (!initialised)
            {
                var e = transform.localEulerAngles;
                yaw = e.y; pitch = e.x > 180f ? e.x - 360f : e.x;
                initialised = true;
            }

            var mouse = Mouse.current;
            if (mouse != null && mouse.rightButton.isPressed)
            {
                var d = mouse.delta.ReadValue();
                yaw += d.x * mouseSensitivity;
                pitch -= d.y * mouseSensitivity;
            }

            var kb = Keyboard.current;
            if (kb != null)
            {
                float dt = Time.deltaTime * keySpeed;
                if (kb.leftArrowKey.isPressed) yaw -= dt;
                if (kb.rightArrowKey.isPressed) yaw += dt;
                if (kb.upArrowKey.isPressed) pitch -= dt;
                if (kb.downArrowKey.isPressed) pitch += dt;
            }

            pitch = Mathf.Clamp(pitch, -89f, 89f);
            transform.localRotation = Quaternion.Euler(pitch, yaw, 0f);
        }
    }
}
