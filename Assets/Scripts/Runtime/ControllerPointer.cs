using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Interactive360
{
    /// <summary>
    /// Laser pointer driven by an XR controller's aim pose (OpenXR). Trigger or primary button selects.
    /// The line is only drawn while a choice panel is open, so it never clutters the film.
    /// Place this object under the same parent as the tracked camera so tracking space matches.
    /// </summary>
    public sealed class ControllerPointer : PointerBase
    {
        public enum Hand { Left, Right }

        public Hand hand = Hand.Right;
        public float lineWidth = 0.004f;
        public Color lineColor = new Color(1f, 1f, 1f, 0.8f);
        public Color lineHitColor = new Color(1f, 0.85f, 0.25f, 0.95f);

        static readonly HashSet<ControllerPointer> tracked = new HashSet<ControllerPointer>();
        public static int TrackedCount => tracked.Count;

        public bool IsTracked { get; private set; }

        InputAction pointerPos, pointerRot, devicePos, deviceRot, isTracked, select;
        LineRenderer line;

        void Awake()
        {
            pointerId = hand == Hand.Left ? 1 : 2;
            string h = hand == Hand.Left ? "{LeftHand}" : "{RightHand}";

            pointerPos = new InputAction("PointerPos", InputActionType.Value, "<XRController>" + h + "/pointerPosition", expectedControlType: "Vector3");
            pointerRot = new InputAction("PointerRot", InputActionType.Value, "<XRController>" + h + "/pointerRotation", expectedControlType: "Quaternion");
            devicePos = new InputAction("DevicePos", InputActionType.Value, "<XRController>" + h + "/devicePosition", expectedControlType: "Vector3");
            deviceRot = new InputAction("DeviceRot", InputActionType.Value, "<XRController>" + h + "/deviceRotation", expectedControlType: "Quaternion");
            isTracked = new InputAction("IsTracked", InputActionType.Value, "<XRController>" + h + "/isTracked");

            select = new InputAction("Select", InputActionType.Button);
            select.AddBinding("<XRController>" + h + "/{TriggerButton}");
            select.AddBinding("<XRController>" + h + "/triggerPressed");
            select.AddBinding("<XRController>" + h + "/{PrimaryButton}");
            select.AddBinding("<XRController>" + h + "/primaryButton");

            line = gameObject.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.positionCount = 2;
            line.startWidth = lineWidth;
            line.endWidth = lineWidth * 0.5f;
            line.numCapVertices = 4;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            var shader = Shader.Find("Sprites/Default");
            if (shader != null) line.material = new Material(shader);
            line.startColor = line.endColor = lineColor;
            line.enabled = false;
        }

        void OnEnable()
        {
            pointerPos.Enable(); pointerRot.Enable(); devicePos.Enable(); deviceRot.Enable(); isTracked.Enable(); select.Enable();
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            pointerPos.Disable(); pointerRot.Disable(); devicePos.Disable(); deviceRot.Disable(); isTracked.Disable(); select.Disable();
            tracked.Remove(this);
            IsTracked = false;
            if (line != null) line.enabled = false;
        }

        void OnDestroy()
        {
            pointerPos.Dispose(); pointerRot.Dispose(); devicePos.Dispose(); deviceRot.Dispose(); isTracked.Dispose(); select.Dispose();
            tracked.Remove(this);
        }

        protected override void Update()
        {
            UpdatePose();
            base.Update();
        }

        void UpdatePose()
        {
            bool hasDevice = devicePos.controls.Count > 0 || pointerPos.controls.Count > 0;
            bool nowTracked = hasDevice && (isTracked.controls.Count == 0 || isTracked.ReadValue<float>() > 0.5f);
            if (nowTracked != IsTracked)
            {
                IsTracked = nowTracked;
                if (IsTracked) tracked.Add(this); else tracked.Remove(this);
            }
            if (!IsTracked) return;

            bool usePointer = pointerPos.controls.Count > 0;
            transform.localPosition = usePointer ? pointerPos.ReadValue<Vector3>() : devicePos.ReadValue<Vector3>();
            var rot = usePointer ? pointerRot.ReadValue<Quaternion>() : deviceRot.ReadValue<Quaternion>();
            if (rot.x != 0f || rot.y != 0f || rot.z != 0f || rot.w != 0f) transform.localRotation = rot;
        }

        protected override bool TryGetRay(out Ray ray)
        {
            ray = new Ray(transform.position, transform.forward);
            return IsTracked;
        }

        protected override bool SelectPressedThisFrame() => select.WasPressedThisFrame();

        protected override void OnRayUpdated(bool active, Ray ray, bool hit, Vector3 point, float dwellNormalized)
        {
            if (line == null) return;
            if (!active) { line.enabled = false; return; }
            line.enabled = true;
            line.SetPosition(0, ray.origin);
            line.SetPosition(1, point);
            var c = (hit && Current != null) ? lineHitColor : lineColor;
            line.startColor = c;
            line.endColor = new Color(c.r, c.g, c.b, hit ? c.a : 0f);
        }
    }
}
