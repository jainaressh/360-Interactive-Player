using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.XR;
using UnityEngine.XR;

namespace Interactive360
{
    /// <summary>
    /// Drives the camera from the HMD centre-eye pose via the Input System's TrackedPoseDriver, with the
    /// actions created in code so no asset wiring is needed. Disables itself when no XR device is active so
    /// DesktopLook can control the camera in the editor.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    [DefaultExecutionOrder(-100)]
    public sealed class HeadTracking : MonoBehaviour
    {
        public int targetFrameRate = 72;

        TrackedPoseDriver driver;
        InputAction position, rotation;

        void Awake()
        {
            position = new InputAction("HeadPosition", InputActionType.Value, "<XRHMD>/centerEyePosition", expectedControlType: "Vector3");
            rotation = new InputAction("HeadRotation", InputActionType.Value, "<XRHMD>/centerEyeRotation", expectedControlType: "Quaternion");

            driver = GetComponent<TrackedPoseDriver>();
            if (driver == null) driver = gameObject.AddComponent<TrackedPoseDriver>();
            driver.positionInput = new InputActionProperty(position);
            driver.rotationInput = new InputActionProperty(rotation);
            driver.trackingType = TrackedPoseDriver.TrackingType.RotationAndPosition;
            driver.updateType = TrackedPoseDriver.UpdateType.UpdateAndBeforeRender;
            driver.enabled = false;

            if (targetFrameRate > 0) Application.targetFrameRate = targetFrameRate;
        }

        void OnEnable() { position.Enable(); rotation.Enable(); }
        void OnDisable() { position.Disable(); rotation.Disable(); if (driver != null) driver.enabled = false; }
        void OnDestroy() { position.Dispose(); rotation.Dispose(); }

        void Update()
        {
            bool xr = XRSettings.isDeviceActive;
            if (driver != null && driver.enabled != xr) driver.enabled = xr;
        }
    }
}
