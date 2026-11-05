using System.Collections;
using TMPro;
using UnityEngine;

namespace FlowerEscape.UI
{
    /// <summary>
    /// Switches the gameplay screen between its two states:
    /// BuyGear (arrange / buy gears) and Combat (the boat fights the wave).
    /// Buttons are wired to the public methods from the Inspector (OnClick).
    /// </summary>
    public class FlowerEscapeUIController : MonoBehaviour
    {
        public enum State { BuyGear, Combat }

        [Header("State roots")]
        public CanvasGroup buyGearState;
        public CanvasGroup combatState;

        [Header("Settings")]
        public State startState = State.BuyGear;
        [Min(0f)] public float fadeDuration = 0.25f;

        [Header("Speed toggle (x2 button)")]
        public TMP_Text[] speedLabels;

        public State Current { get; private set; }

        private Coroutine _transition;
        private bool _fastForward;

        private void Start()
        {
            SetStateImmediate(startState);
            RefreshSpeedLabels();
        }

        public void ShowCombat() => GoTo(State.Combat);
        public void ShowBuyGear() => GoTo(State.BuyGear);

        public void ToggleState() => GoTo(Current == State.BuyGear ? State.Combat : State.BuyGear);

        public void ToggleSpeed()
        {
            _fastForward = !_fastForward;
            Time.timeScale = _fastForward ? 2f : 1f;
            RefreshSpeedLabels();
        }

        public void GoTo(State target)
        {
            if (target == Current && _transition == null) return;
            if (_transition != null) StopCoroutine(_transition);
            _transition = StartCoroutine(CrossFade(target));
        }

        public void SetStateImmediate(State target)
        {
            Current = target;
            Apply(buyGearState, target == State.BuyGear ? 1f : 0f);
            Apply(combatState, target == State.Combat ? 1f : 0f);
        }

        private IEnumerator CrossFade(State target)
        {
            var from = Current == State.BuyGear ? buyGearState : combatState;
            var to = target == State.BuyGear ? buyGearState : combatState;
            Current = target;

            if (to) { to.gameObject.SetActive(true); to.blocksRaycasts = false; }
            if (from) from.blocksRaycasts = false;

            float t = 0f;
            while (t < fadeDuration)
            {
                t += Time.unscaledDeltaTime;
                float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / fadeDuration));
                if (from) from.alpha = 1f - k;
                if (to) to.alpha = k;
                yield return null;
            }

            Apply(from, 0f);
            Apply(to, 1f);
            _transition = null;
        }

        private static void Apply(CanvasGroup group, float alpha)
        {
            if (!group) return;
            bool visible = alpha > 0.5f;
            group.alpha = alpha;
            group.interactable = visible;
            group.blocksRaycasts = visible;
            group.gameObject.SetActive(visible);
        }

        private void RefreshSpeedLabels()
        {
            if (speedLabels == null) return;
            foreach (var label in speedLabels)
                if (label) label.text = _fastForward ? "x1" : "x2";
        }

        private void OnDisable()
        {
            Time.timeScale = 1f;
        }
    }
}
