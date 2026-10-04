using UnityEngine;

namespace Interactive360
{
    /// <summary>
    /// Shared hover/select loop for gaze and controller pointers. Subclasses supply the ray and the
    /// select input; this class raycasts against ChoiceButton colliders and drives hover, dwell and selection.
    /// </summary>
    public abstract class PointerBase : MonoBehaviour
    {
        [Tooltip("0 = gaze, 1 = left controller, 2 = right controller. Must be unique per pointer.")]
        public int pointerId;
        public float maxDistance = 20f;
        public LayerMask layerMask = ~0;

        protected ChoiceButton Current { get; private set; }
        protected bool Active { get; private set; }
        float dwell;

        protected abstract bool TryGetRay(out Ray ray);
        protected abstract bool SelectPressedThisFrame();

        /// <summary>Seconds of steady hover that count as a selection. 0 disables dwell for this pointer.</summary>
        protected virtual float DwellSeconds => 0f;

        /// <summary>Called every frame with the latest ray info so subclasses can draw a reticle or line.</summary>
        protected virtual void OnRayUpdated(bool active, Ray ray, bool hit, Vector3 point, float dwellNormalized) { }

        protected virtual void Update()
        {
            var ui = ChoiceUI.Instance;
            Ray ray = default;
            Active = ui != null && ui.IsVisible && !ui.Locked && TryGetRay(out ray);
            if (!Active)
            {
                SetCurrent(null);
                OnRayUpdated(false, ray, false, Vector3.zero, 0f);
                return;
            }

            bool hit = Physics.Raycast(ray, out var info, maxDistance, layerMask, QueryTriggerInteraction.Collide);
            var button = hit ? info.collider.GetComponentInParent<ChoiceButton>() : null;
            SetCurrent(button);

            float dwellNormalized = 0f;
            if (Current != null)
            {
                if (SelectPressedThisFrame())
                {
                    ui.Select(Current);
                }
                else if (DwellSeconds > 0f)
                {
                    dwell += Time.deltaTime;
                    dwellNormalized = Mathf.Clamp01(dwell / DwellSeconds);
                    Current.SetDwell(dwellNormalized);
                    if (dwell >= DwellSeconds) ui.Select(Current);
                }
            }

            OnRayUpdated(true, ray, hit, hit ? info.point : ray.GetPoint(Mathf.Min(maxDistance, 5f)), dwellNormalized);
        }

        void SetCurrent(ChoiceButton button)
        {
            if (button == Current) return;
            if (Current != null) Current.SetHover(pointerId, false);
            Current = button;
            dwell = 0f;
            if (Current != null) Current.SetHover(pointerId, true);
        }

        protected virtual void OnDisable()
        {
            SetCurrent(null);
            Active = false;
        }
    }
}
