using UnityEngine;

namespace HouseFlip.Polish
{
    /// <summary>
    /// Brief colour flash when an object takes a hit (GDD 18 — Polish).
    ///
    /// Written through a MaterialPropertyBlock so it never instances a material, which
    /// matters here: a wall being hammered would otherwise leak a new material per swing.
    /// It also composes with <see cref="Painting.PaintableWall"/>, which writes the same
    /// property — the flash restores the wall's colour when it finishes rather than
    /// assuming white.
    /// </summary>
    public class HitFlash : MonoBehaviour
    {
        [SerializeField] private Color flashColor = new Color(1f, 0.95f, 0.75f);
        [SerializeField] private float duration = 0.14f;

        private Renderer[] _renderers;
        private MaterialPropertyBlock _block;
        private float _remaining;
        private Color _restoreColor = Color.white;
        private bool _flashing;

        private void Awake()
        {
            _renderers = GetComponentsInChildren<Renderer>(true);
            _block = new MaterialPropertyBlock();
        }

        /// <summary>Flashes, then returns the renderers to <paramref name="restoreColor"/>.</summary>
        public void Flash(Color restoreColor)
        {
            _restoreColor = restoreColor;
            _remaining = duration;
            _flashing = true;
            Apply(flashColor);
        }

        private void Update()
        {
            if (!_flashing)
            {
                return;
            }

            _remaining -= Time.deltaTime;
            if (_remaining > 0f)
            {
                return;
            }

            _flashing = false;
            Apply(_restoreColor);
        }

        private void Apply(Color color)
        {
            if (_renderers == null)
            {
                return;
            }

            foreach (Renderer renderer in _renderers)
            {
                if (renderer == null)
                {
                    continue;
                }

                renderer.GetPropertyBlock(_block);
                _block.SetColor("_Color", color);
                _block.SetColor("_BaseColor", color);
                renderer.SetPropertyBlock(_block);
            }
        }

        public static void FlashOn(GameObject target, Color restoreColor)
        {
            if (target == null)
            {
                return;
            }

            var flash = target.GetComponent<HitFlash>();
            if (flash == null)
            {
                flash = target.AddComponent<HitFlash>();
            }

            flash.Flash(restoreColor);
        }
    }
}
