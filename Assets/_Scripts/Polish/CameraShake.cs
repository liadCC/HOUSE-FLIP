using UnityEngine;

namespace HouseFlip.Polish
{
    /// <summary>
    /// Trauma-based camera shake (GDD 18 — Polish).
    ///
    /// Shake is driven by a 0–1 "trauma" value that decays every frame, and offsets scale
    /// with trauma squared. That curve is what makes a big hit read as violent while a
    /// dozen small ones stay comfortable — linear trauma feels like a permanent wobble.
    ///
    /// Applied as a post-pass on the camera transform, after the rig has positioned it,
    /// so it never fights the follow logic.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class CameraShake : MonoBehaviour
    {
        public static CameraShake Instance { get; private set; }

        [SerializeField] private float maxPositionOffset = 0.35f;
        [SerializeField] private float maxRotationOffset = 2.4f;

        [Tooltip("Trauma lost per second. Higher decays faster.")]
        [SerializeField] private float decayPerSecond = 1.6f;

        [Tooltip("How quickly the noise scrolls. Higher is a sharper rattle.")]
        [SerializeField] private float frequency = 26f;

        private float _trauma;
        private float _seed;

        private Vector3 _appliedOffset;
        private Quaternion _appliedRotation = Quaternion.identity;

        private void Awake()
        {
            Instance = this;
            _seed = Random.Range(0f, 100f);
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        /// <summary>Adds trauma. Values stack but never exceed 1.</summary>
        public void AddTrauma(float amount)
        {
            _trauma = Mathf.Clamp01(_trauma + Mathf.Max(0f, amount));
        }

        private void LateUpdate()
        {
            // Undo last frame's shake first: the rig writes a clean transform each
            // LateUpdate, but component order is not guaranteed, so we cannot assume it.
            transform.localPosition -= _appliedOffset;
            _appliedOffset = Vector3.zero;
            _appliedRotation = Quaternion.identity;

            if (_trauma <= 0f)
            {
                return;
            }

            _trauma = Mathf.Max(0f, _trauma - decayPerSecond * Time.deltaTime);

            float shake = _trauma * _trauma;
            float t = Time.time * frequency;

            // Perlin noise rather than Random: consecutive samples are correlated, which
            // reads as a shake instead of per-frame static.
            float x = (Mathf.PerlinNoise(_seed, t) - 0.5f) * 2f;
            float y = (Mathf.PerlinNoise(_seed + 17f, t) - 0.5f) * 2f;
            float roll = (Mathf.PerlinNoise(_seed + 43f, t) - 0.5f) * 2f;

            _appliedOffset = new Vector3(x, y, 0f) * (maxPositionOffset * shake);
            _appliedRotation = Quaternion.Euler(0f, 0f, roll * maxRotationOffset * shake);

            transform.localPosition += _appliedOffset;
            transform.localRotation *= _appliedRotation;
        }

        /// <summary>Convenience for callers that do not want to null-check.</summary>
        public static void Shake(float trauma)
        {
            if (Instance != null)
            {
                Instance.AddTrauma(trauma);
            }
        }
    }
}
