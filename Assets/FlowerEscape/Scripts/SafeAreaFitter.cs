using UnityEngine;

namespace FlowerEscape.UI
{
    /// <summary>
    /// Keeps interactive UI inside Screen.safeArea (notches / home indicator on tall 9:21 phones).
    /// The background stays outside this rect so it still bleeds edge to edge.
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(RectTransform))]
    public class SafeAreaFitter : MonoBehaviour
    {
        private Rect _applied;
        private Vector2Int _screen;

        private void OnEnable() => Apply();
        private void Update()
        {
            if (_applied != Screen.safeArea || _screen.x != Screen.width || _screen.y != Screen.height) Apply();
        }

        private void Apply()
        {
            var rt = (RectTransform)transform;
            _applied = Screen.safeArea;
            _screen = new Vector2Int(Screen.width, Screen.height);
            if (!Application.isPlaying || Screen.width <= 0 || Screen.height <= 0)
            {
                rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
                rt.offsetMin = rt.offsetMax = Vector2.zero;
                return;
            }
            var min = _applied.position;
            var max = _applied.position + _applied.size;
            rt.anchorMin = new Vector2(min.x / Screen.width, min.y / Screen.height);
            rt.anchorMax = new Vector2(max.x / Screen.width, max.y / Screen.height);
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }
    }
}
