using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UnityEngine.XR;

namespace Interactive360
{
    /// <summary>
    /// Head-gaze pointer with a reticle. In the headset it selects with any trigger/primary button, or by dwell.
    /// Both are disabled while a controller is tracked and pointing (ControllerPointer takes over).
    /// On desktop (no HMD) it becomes a mouse pointer: click or Space to select.
    /// </summary>
    public sealed class GazePointer : PointerBase
    {
        public Camera head;

        [Header("Dwell")]
        [Tooltip("Look at an option this long to pick it. 0 disables dwell.")]
        public float dwellSeconds = 2.0f;
        public bool dwellOnlyWithoutControllers = true;

        [Header("Reticle")]
        public float reticleDistance = 2.0f;
        public float reticleSizeMeters = 0.06f;

        InputAction select;
        GameObject reticle;
        Image ring;
        Image dot;

        protected override float DwellSeconds =>
            (dwellOnlyWithoutControllers && ControllerPointer.TrackedCount > 0) ? 0f : dwellSeconds;

        void Awake()
        {
            if (head == null) head = GetComponentInParent<Camera>() ?? Camera.main;
            pointerId = 0;

            select = new InputAction("GazeSelect", InputActionType.Button);
            select.AddBinding("<XRController>/{TriggerButton}");
            select.AddBinding("<XRController>/triggerPressed");
            select.AddBinding("<XRController>/{PrimaryButton}");
            select.AddBinding("<XRController>/primaryButton");
            select.AddBinding("<Keyboard>/space");
            select.AddBinding("<Keyboard>/enter");
            select.AddBinding("<Mouse>/leftButton");

            BuildReticle();
        }

        void BuildReticle()
        {
            if (head == null) return;
            reticle = new GameObject("Reticle", typeof(RectTransform), typeof(Canvas));
            reticle.transform.SetParent(head.transform, false);
            var canvas = reticle.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 30;
            var rt = (RectTransform)reticle.transform;
            rt.sizeDelta = new Vector2(100f, 100f);
            rt.localPosition = new Vector3(0f, 0f, reticleDistance);
            rt.localRotation = Quaternion.identity;
            rt.localScale = Vector3.one * (reticleSizeMeters / 100f);

            var back = UiFactory.Image("Back", rt, new Color(1f, 1f, 1f, 0.25f), UiFactory.Circle);
            back.type = Image.Type.Simple;
            UiFactory.Stretch((RectTransform)back.transform);

            ring = UiFactory.Image("Ring", rt, new Color(1f, 0.85f, 0.25f, 0.95f), UiFactory.Circle);
            UiFactory.Stretch((RectTransform)ring.transform);
            ring.type = Image.Type.Filled;
            ring.fillMethod = Image.FillMethod.Radial360;
            ring.fillOrigin = (int)Image.Origin360.Top;
            ring.fillClockwise = true;
            ring.fillAmount = 0f;

            dot = UiFactory.Image("Dot", rt, Color.white, UiFactory.Circle);
            dot.type = Image.Type.Simple;
            var dr = (RectTransform)dot.transform;
            dr.anchorMin = dr.anchorMax = new Vector2(0.5f, 0.5f);
            dr.sizeDelta = new Vector2(22f, 22f);

            reticle.SetActive(false);
        }

        void OnEnable() { select?.Enable(); }
        protected override void OnDisable() { base.OnDisable(); select?.Disable(); if (reticle != null) reticle.SetActive(false); }
        void OnDestroy() { select?.Dispose(); }

        static bool XrActive => XRSettings.isDeviceActive;

        protected override bool TryGetRay(out Ray ray)
        {
            if (head == null) { ray = default; return false; }
            if (XrActive)
            {
                ray = new Ray(head.transform.position, head.transform.forward);
                return true;
            }
            var mouse = Mouse.current;
            if (mouse == null) { ray = new Ray(head.transform.position, head.transform.forward); return true; }
            ray = head.ScreenPointToRay(mouse.position.ReadValue());
            return true;
        }

        protected override bool SelectPressedThisFrame()
        {
            if (XrActive && ControllerPointer.TrackedCount > 0) return false; // controllers own the trigger
            return select.WasPressedThisFrame();
        }

        protected override void OnRayUpdated(bool active, Ray ray, bool hit, Vector3 point, float dwellNormalized)
        {
            if (reticle == null) return;
            bool show = active && XrActive && ControllerPointer.TrackedCount == 0;
            if (reticle.activeSelf != show) reticle.SetActive(show);
            if (!show) return;

            float d = hit ? Mathf.Clamp(Vector3.Distance(head.transform.position, point) - 0.05f, 0.5f, reticleDistance) : reticleDistance;
            var rt = (RectTransform)reticle.transform;
            rt.localPosition = new Vector3(0f, 0f, d);
            rt.localScale = Vector3.one * (reticleSizeMeters / 100f) * (d / reticleDistance);
            ring.fillAmount = dwellNormalized;
            dot.color = Current != null ? new Color(1f, 0.85f, 0.25f, 1f) : Color.white;
        }
    }
}
