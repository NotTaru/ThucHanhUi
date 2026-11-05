using UnityEngine;

namespace FlowerEscape.UI
{
    /// <summary>
    /// Keeps the square environment background top-anchored at its design size (874x874),
    /// and grows it uniformly only when the canvas is wider than that, so it always covers the screen width.
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(RectTransform))]
    public class BackgroundCover : MonoBehaviour
    {
        public float designSize = 874f;
        private Vector2 _lastParent;

        private void OnEnable() => Fit();
        private void Update()
        {
            var parent = transform.parent as RectTransform;
            if (parent && parent.rect.size != _lastParent) Fit();
        }

        private void Fit()
        {
            var rt = (RectTransform)transform;
            var parent = transform.parent as RectTransform;
            if (!parent) return;
            _lastParent = parent.rect.size;
            // Only grow for wider screens: on taller screens the extra height sits under the
            // bottom-anchored board / ad banner, and the water line must stay where the battlefield expects it.
            float size = Mathf.Max(designSize, parent.rect.width);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.sizeDelta = new Vector2(size, size);
            rt.anchoredPosition = Vector2.zero;
        }
    }
}
