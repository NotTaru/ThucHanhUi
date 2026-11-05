using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace FlowerEscape.UI
{
    /// <summary>Squash-on-press feedback for game buttons (scales the whole button group).</summary>
    [RequireComponent(typeof(RectTransform))]
    public class UIPressScale : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        [Range(0.5f, 1f)] public float pressedScale = 0.9f;
        [Min(1f)] public float speed = 18f;

        private Vector3 _baseScale = Vector3.one;
        private float _target = 1f;
        private float _current = 1f;
        private Selectable _selectable;

        private void Awake()
        {
            _baseScale = transform.localScale;
            _selectable = GetComponent<Selectable>();
        }

        private void OnDisable()
        {
            _target = _current = 1f;
            transform.localScale = _baseScale;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (_selectable && !_selectable.IsInteractable()) return;
            _target = pressedScale;
        }

        public void OnPointerUp(PointerEventData eventData) => _target = 1f;
        public void OnPointerExit(PointerEventData eventData) => _target = 1f;

        private void Update()
        {
            if (Mathf.Approximately(_current, _target)) return;
            _current = Mathf.MoveTowards(_current, _target, speed * Time.unscaledDeltaTime);
            transform.localScale = _baseScale * _current;
        }
    }
}
