using System.Collections.Generic;
using UnityEngine;

namespace HouseFlip.Events
{
    /// <summary>
    /// EVENT 2 — POWER OUTAGE (GDD 21).
    ///
    /// The lights go out and the ambient light drops until someone finds the fuse box and
    /// gets it running again with a screwdriver. Working in the dark is the punishment;
    /// there is no score penalty on top of it.
    /// </summary>
    public class PowerOutageEvent : RandomEventHandler
    {
        [SerializeField] private FuseBox fuseBox;

        [Header("Darkness")]
        [SerializeField] private Color blackoutAmbient = new Color(0.06f, 0.06f, 0.10f);
        [SerializeField] private float blackoutLightScale = 0.12f;
        [SerializeField] private float fadeSpeed = 2.5f;

        private readonly List<Light> _houseLights = new List<Light>();
        private readonly List<float> _originalIntensities = new List<float>();

        private Color _originalAmbient;
        private float _originalAmbientIntensity;
        private bool _cached;
        private float _blend;

        private void Awake()
        {
            if (fuseBox == null)
            {
                fuseBox = FindFirstObjectByType<FuseBox>();
            }
        }

        private void CacheLighting()
        {
            if (_cached)
            {
                return;
            }

            _originalAmbient = RenderSettings.ambientLight;
            _originalAmbientIntensity = RenderSettings.ambientIntensity;

            foreach (Light light in FindObjectsByType<Light>(FindObjectsSortMode.None))
            {
                // Leave the sun alone — killing it makes the yard unplayable rather than dark.
                if (light.type == LightType.Directional)
                {
                    continue;
                }

                _houseLights.Add(light);
                _originalIntensities.Add(light.intensity);
            }

            _cached = true;
        }

        protected override void OnServerBegin()
        {
            if (fuseBox == null)
            {
                Debug.LogWarning("[PowerOutageEvent] No FuseBox in the scene — event cannot be resolved.");
                return;
            }

            fuseBox.Restored += OnPowerRestored;
            fuseBox.ServerSetTripped(true);
        }

        private void OnPowerRestored(FuseBox box, ulong resolverClientId)
        {
            ServerResolve(true, resolverClientId);
        }

        protected override void OnServerResolve(bool playersFixedIt, ulong resolverClientId)
        {
            if (fuseBox == null)
            {
                return;
            }

            fuseBox.Restored -= OnPowerRestored;
            fuseBox.ServerSetTripped(false);
        }

        protected override void ApplyActiveState(bool active)
        {
            CacheLighting();
        }

        private void Update()
        {
            // Runs on every peer: IsActive is replicated, so the fade is identical for all.
            float target = IsActive ? 1f : 0f;
            if (Mathf.Approximately(_blend, target))
            {
                return;
            }

            CacheLighting();
            _blend = Mathf.MoveTowards(_blend, target, fadeSpeed * Time.deltaTime);

            RenderSettings.ambientLight = Color.Lerp(_originalAmbient, blackoutAmbient, _blend);
            RenderSettings.ambientIntensity = Mathf.Lerp(_originalAmbientIntensity, 0.15f, _blend);

            for (int i = 0; i < _houseLights.Count; i++)
            {
                Light light = _houseLights[i];
                if (light == null)
                {
                    continue;
                }

                light.intensity = Mathf.Lerp(
                    _originalIntensities[i], _originalIntensities[i] * blackoutLightScale, _blend);
            }
        }

        public void SetFuseBox(FuseBox box) => fuseBox = box;
    }
}
