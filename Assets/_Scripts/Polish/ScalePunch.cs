using UnityEngine;

namespace HouseFlip.Polish
{
    /// <summary>
    /// Squash-and-stretch punch on a transform (GDD 18 — Polish).
    ///
    /// Used on HUD numbers when the budget or house value changes, and on freshly placed
    /// furniture so it lands with a bit of weight instead of just appearing.
    ///
    /// The overshoot curve matters: easing straight back to 1 reads as a fade, while
    /// crossing below the resting scale on the way out reads as a bounce.
    /// </summary>
    public class ScalePunch : MonoBehaviour
    {
        [SerializeField] private float duration = 0.28f;
        [SerializeField] private float defaultAmount = 0.22f;

        private Vector3 _baseScale = Vector3.one;
        private bool _captured;
        private float _elapsed;
        private float _amount;
        private bool _playing;

        private void Awake() => Capture();

        private void Capture()
        {
            if (!_captured)
            {
                _baseScale = transform.localScale;
                _captured = true;
            }
        }

        public void Punch() => Punch(defaultAmount);

        public void Punch(float amount)
        {
            Capture();
            _amount = Mathf.Max(0.01f, amount);
            _elapsed = 0f;
            _playing = true;
        }

        private void Update()
        {
            if (!_playing)
            {
                return;
            }

            _elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(_elapsed / Mathf.Max(0.01f, duration));

            if (t >= 1f)
            {
                _playing = false;
                transform.localScale = _baseScale;
                return;
            }

            // Damped sine: one clear overshoot, one small undershoot, then settle.
            float damping = 1f - t;
            float wave = Mathf.Sin(t * Mathf.PI * 2.2f) * damping * damping;

            transform.localScale = _baseScale * (1f + wave * _amount);
        }

        /// <summary>Adds the component if needed and punches in one call.</summary>
        public static void PunchOn(GameObject target, float amount)
        {
            if (target == null)
            {
                return;
            }

            var punch = target.GetComponent<ScalePunch>();
            if (punch == null)
            {
                punch = target.AddComponent<ScalePunch>();
            }

            punch.Punch(amount);
        }
    }
}
