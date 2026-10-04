using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Interactive360
{
    /// <summary>One selectable option on the choice panel. Pointers hover/select it via its BoxCollider.</summary>
    public sealed class ChoiceButton : MonoBehaviour
    {
        public int Index { get; private set; }
        public Image Background { get; private set; }
        public Text Label { get; private set; }
        public Image DwellFill { get; private set; }
        public BoxCollider Collider { get; private set; }
        public bool Selected { get; private set; }
        public bool IsHovered => hoverers.Count > 0;

        readonly HashSet<int> hoverers = new HashSet<int>();

        public static readonly Color NormalColor = new Color(0.10f, 0.10f, 0.12f, 0.92f);
        public static readonly Color HoverColor = new Color(0.30f, 0.30f, 0.36f, 0.97f);
        public static readonly Color SelectedColor = new Color(0.92f, 0.78f, 0.20f, 1f);
        public static readonly Color DimColor = new Color(0.10f, 0.10f, 0.12f, 0.45f);

        public void Init(int index, Image background, Text label, Image dwellFill, BoxCollider collider)
        {
            Index = index; Background = background; Label = label; DwellFill = dwellFill; Collider = collider;
            Selected = false;
            hoverers.Clear();
            SetDwell(0f);
            Refresh();
        }

        public void SetHover(int pointerId, bool hovering)
        {
            if (hovering) hoverers.Add(pointerId); else hoverers.Remove(pointerId);
            if (!IsHovered) SetDwell(0f);
            Refresh();
        }

        public void ClearHover() { hoverers.Clear(); SetDwell(0f); Refresh(); }

        public void SetDwell(float normalized)
        {
            if (DwellFill != null) DwellFill.fillAmount = Mathf.Clamp01(normalized);
        }

        public void MarkSelected(bool selected)
        {
            Selected = selected;
            SetDwell(0f);
            Refresh();
        }

        public void Dim()
        {
            if (Background != null) Background.color = DimColor;
            if (Label != null) Label.color = new Color(1f, 1f, 1f, 0.5f);
            transform.localScale = Vector3.one;
        }

        void Refresh()
        {
            if (Background == null) return;
            Background.color = Selected ? SelectedColor : IsHovered ? HoverColor : NormalColor;
            if (Label != null) Label.color = Selected ? new Color(0.08f, 0.08f, 0.08f, 1f) : Color.white;
            transform.localScale = (IsHovered && !Selected) ? Vector3.one * 1.04f : Vector3.one;
        }

        /// <summary>Match the collider to the laid-out rect (call after layout rebuild).</summary>
        public void FitCollider()
        {
            if (Collider == null) return;
            var rt = (RectTransform)transform;
            var size = rt.rect.size;
            Collider.center = new Vector3((0.5f - rt.pivot.x) * size.x, (0.5f - rt.pivot.y) * size.y, 0f);
            Collider.size = new Vector3(size.x, size.y, 40f);
        }
    }
}
