using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Interactive360
{
    /// <summary>
    /// World-space choice panel (built at runtime) plus a small status panel for loading/errors/"THE END".
    /// The panel is placed in front of the viewer when it appears and only re-centres if they look well away.
    /// </summary>
    public sealed class ChoiceUI : MonoBehaviour
    {
        public static ChoiceUI Instance { get; private set; }

        [Header("Placement")]
        public float distance = 2.6f;
        public float heightOffset = -0.45f;
        [Tooltip("Re-centre the panel when the viewer looks more than this many degrees away from it.")]
        public float recenterAngle = 55f;
        public float followSpeed = 3f;

        /// <summary>Raised once when the viewer picks an option (index into the labels passed to Show).</summary>
        public event Action<int> Chosen;

        public bool IsVisible => panel != null && panel.activeSelf;
        public bool Locked { get; private set; }
        public IReadOnlyList<ChoiceButton> Buttons => buttons;

        const float Scale = 0.001f; // 1 canvas unit = 1 mm
        const float PanelW = 1900f, PanelH = 520f;

        GameObject panel;
        RectTransform panelRect;
        Text promptText;
        RectTransform row;
        GameObject timerRoot;
        Image timerFill;
        readonly List<ChoiceButton> buttons = new List<ChoiceButton>();
        bool recentering;

        GameObject status;
        RectTransform statusRect;
        Text statusText;
        float statusHideAt = float.PositiveInfinity;

        Transform head;

        void Awake()
        {
            Instance = this;
            BuildPanel();
            BuildStatus();
            panel.SetActive(false);
            status.SetActive(false);
        }

        void OnDestroy() { if (Instance == this) Instance = null; }

        void BuildPanel()
        {
            panel = new GameObject("ChoicePanel", typeof(RectTransform), typeof(Canvas));
            panel.transform.SetParent(transform, false);
            var canvas = panel.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 10;
            panelRect = (RectTransform)panel.transform;
            panelRect.sizeDelta = new Vector2(PanelW, PanelH);
            panelRect.localScale = Vector3.one * Scale;

            var bg = UiFactory.Image("Background", panelRect, new Color(0f, 0f, 0f, 0.55f), UiFactory.Rounded);
            UiFactory.Stretch((RectTransform)bg.transform);

            promptText = UiFactory.Text("Prompt", panelRect, "", 44, new Color(1f, 1f, 1f, 0.85f));
            var pr = (RectTransform)promptText.transform;
            pr.anchorMin = new Vector2(0f, 1f); pr.anchorMax = new Vector2(1f, 1f); pr.pivot = new Vector2(0.5f, 1f);
            pr.anchoredPosition = new Vector2(0f, -22f); pr.sizeDelta = new Vector2(-80f, 70f);

            row = UiFactory.Rect("Choices", panelRect);
            row.anchorMin = Vector2.zero; row.anchorMax = Vector2.one;
            row.offsetMin = new Vector2(40f, 72f); row.offsetMax = new Vector2(-40f, -100f);
            var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 40f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childForceExpandWidth = true; layout.childForceExpandHeight = true;
            layout.childControlWidth = true; layout.childControlHeight = true;

            var timerBg = UiFactory.Image("Timer", panelRect, new Color(1f, 1f, 1f, 0.15f));
            timerRoot = timerBg.gameObject;
            var tr = (RectTransform)timerBg.transform;
            tr.anchorMin = new Vector2(0f, 0f); tr.anchorMax = new Vector2(1f, 0f); tr.pivot = new Vector2(0.5f, 0f);
            tr.anchoredPosition = new Vector2(0f, 30f); tr.sizeDelta = new Vector2(-80f, 14f);
            timerFill = UiFactory.Image("Fill", tr, new Color(1f, 0.85f, 0.25f, 1f));
            UiFactory.Stretch((RectTransform)timerFill.transform);
            timerFill.type = Image.Type.Filled;
            timerFill.fillMethod = Image.FillMethod.Horizontal;
            timerFill.fillOrigin = (int)Image.OriginHorizontal.Left;
            timerFill.fillAmount = 1f;
        }

        void BuildStatus()
        {
            status = new GameObject("StatusPanel", typeof(RectTransform), typeof(Canvas));
            status.transform.SetParent(transform, false);
            var canvas = status.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 20;
            statusRect = (RectTransform)status.transform;
            statusRect.sizeDelta = new Vector2(1400f, 320f);
            statusRect.localScale = Vector3.one * Scale;

            var bg = UiFactory.Image("Background", statusRect, new Color(0f, 0f, 0f, 0.6f), UiFactory.Rounded);
            UiFactory.Stretch((RectTransform)bg.transform);
            statusText = UiFactory.Text("Text", statusRect, "", 56, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
            UiFactory.Stretch((RectTransform)statusText.transform, 40f);
            statusText.resizeTextForBestFit = true;
            statusText.resizeTextMinSize = 24;
            statusText.resizeTextMaxSize = 64;
        }

        /// <summary>Show the options in front of the viewer. Index of <paramref name="labels"/> is reported through Chosen.</summary>
        public void Show(string prompt, IList<string> labels, Transform headTransform)
        {
            head = headTransform != null ? headTransform : head;
            Locked = false;
            recentering = false;
            promptText.text = prompt ?? string.Empty;

            foreach (var b in buttons) if (b != null) Destroy(b.gameObject);
            buttons.Clear();
            for (int i = 0; i < labels.Count; i++) CreateButton(i, labels[i]);

            panel.SetActive(true);
            Place(panelRect, true);
            LayoutRebuilder.ForceRebuildLayoutImmediate(row);
            foreach (var b in buttons) b.FitCollider();
            SetProgress(1f);
        }

        ChoiceButton CreateButton(int index, string label)
        {
            var img = UiFactory.Image("Choice" + index, row, ChoiceButton.NormalColor, UiFactory.Rounded);
            var rt = (RectTransform)img.transform;
            var le = rt.gameObject.AddComponent<LayoutElement>();
            le.preferredHeight = 240f;
            le.flexibleWidth = 1f;

            var text = UiFactory.Text("Label", rt, label, 52, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
            UiFactory.Stretch((RectTransform)text.transform, 28f);
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = 26;
            text.resizeTextMaxSize = 56;

            var dwell = UiFactory.Image("Dwell", rt, new Color(1f, 1f, 1f, 0.9f));
            var dr = (RectTransform)dwell.transform;
            dr.anchorMin = new Vector2(0f, 0f); dr.anchorMax = new Vector2(1f, 0f); dr.pivot = new Vector2(0.5f, 0f);
            dr.anchoredPosition = Vector2.zero; dr.sizeDelta = new Vector2(0f, 10f);
            dwell.type = Image.Type.Filled;
            dwell.fillMethod = Image.FillMethod.Horizontal;
            dwell.fillOrigin = (int)Image.OriginHorizontal.Left;
            dwell.fillAmount = 0f;

            var collider = rt.gameObject.AddComponent<BoxCollider>();
            collider.isTrigger = true;

            var button = rt.gameObject.AddComponent<ChoiceButton>();
            button.Init(index, img, text, dwell, collider);
            buttons.Add(button);
            return button;
        }

        /// <summary>1 = window just opened, 0 = closing. Negative hides the timer bar (hold-last-frame nodes).</summary>
        public void SetProgress(float normalized)
        {
            if (normalized < 0f) { timerRoot.SetActive(false); return; }
            timerRoot.SetActive(true);
            timerFill.fillAmount = Mathf.Clamp01(normalized);
        }

        /// <summary>Called by pointers. Fires Chosen once; the story player then calls MarkSelected.</summary>
        public void Select(ChoiceButton button)
        {
            if (Locked || button == null || !IsVisible) return;
            Locked = true;
            Chosen?.Invoke(button.Index);
        }

        public void MarkSelected(int index)
        {
            Locked = true;
            foreach (var b in buttons)
            {
                if (b.Index == index) b.MarkSelected(true); else b.Dim();
            }
            timerRoot.SetActive(false);
        }

        public void Hide()
        {
            foreach (var b in buttons) if (b != null) b.ClearHover();
            panel.SetActive(false);
            Locked = false;
        }

        public void ShowStatus(string text, float seconds = 0f)
        {
            statusText.text = text ?? string.Empty;
            status.SetActive(true);
            Place(statusRect, true);
            statusHideAt = seconds > 0f ? Time.time + seconds : float.PositiveInfinity;
        }

        public void HideStatus() { status.SetActive(false); statusHideAt = float.PositiveInfinity; }

        void LateUpdate()
        {
            if (head == null && Camera.main != null) head = Camera.main.transform;
            if (head == null) return;

            if (IsVisible) Place(panelRect, false);
            if (status.activeSelf)
            {
                if (Time.time >= statusHideAt) HideStatus();
                else
                {
                    var target = TargetPosition();
                    statusRect.position = Vector3.Lerp(statusRect.position, target, Time.deltaTime * followSpeed * 2f);
                    Face(statusRect);
                }
            }
        }

        Vector3 Forward()
        {
            var fwd = head.forward; fwd.y = 0f;
            if (fwd.sqrMagnitude < 1e-4f) fwd = Vector3.forward;
            return fwd.normalized;
        }

        Vector3 TargetPosition() => head.position + Forward() * distance + Vector3.up * heightOffset;

        void Place(RectTransform rt, bool immediate)
        {
            if (head == null) return;
            var target = TargetPosition();
            if (immediate)
            {
                rt.position = target;
                recentering = false;
            }
            else
            {
                var toPanel = rt.position - head.position; toPanel.y = 0f;
                if (!recentering && Vector3.Angle(toPanel, Forward()) > recenterAngle) recentering = true;
                if (recentering)
                {
                    rt.position = Vector3.Lerp(rt.position, target, Time.deltaTime * followSpeed);
                    if ((rt.position - target).sqrMagnitude < 0.0004f) recentering = false;
                }
            }
            Face(rt);
        }

        void Face(RectTransform rt)
        {
            var dir = rt.position - head.position;
            if (dir.sqrMagnitude > 1e-6f) rt.rotation = Quaternion.LookRotation(dir, Vector3.up);
        }
    }
}
