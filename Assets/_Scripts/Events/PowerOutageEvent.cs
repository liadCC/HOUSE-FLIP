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
        private bool _ambientCached;
        private float _blend;

        private void Awake()
        {
            if (fuseBox == null)
            {
                fuseBox = FindFirstObjectByType<FuseBox>();
            }
        }

        private void CacheAmbient()
        {
            if (_ambientCached)
            {
                return;
            }

            _originalAmbient = RenderSettings.ambientLight;
            _originalAmbientIntensity = RenderSettings.ambientIntensity;
            _ambientCached = true;
        }

        /// <summary>
        /// Sampled on the rising edge of each blackout rather than once, because this
        /// handler spawns with the scene — in the lobby, before the round exists. A cache
        /// taken then can never contain the lamps players buy and place during the round,
        /// so those lights would keep burning through a "power outage".
        /// </summary>
        private void CaptureLights()
        {
            _houseLights.Clear();
            _originalIntensities.Clear();

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
            CacheAmbient();

            // Intensities are only meaningful while the scene is still at full brightness;
            // re-sampling part-way through a fade would bake the dimmed values in as the
            // originals and the lights could never be brought back up.
            if (active && _blend <= 0f)
            {
                CaptureLights();
            }
        }

        public override void OnNetworkDespawn()
        {
            base.OnNetworkDespawn();
            RestoreLighting();
        }

        public override void OnDestroy()
        {
            RestoreLighting();
            base.OnDestroy();
        }

        /// <summary>
        /// RenderSettings is global and outlives this component, and the fade in
        /// <see cref="Update"/> is the only thing that ever walks it back. Losing the
        /// handler mid-outage — session shutdown, disconnect, scene teardown — would
        /// otherwise strand the scene at blackout values for good, and a later instance
        /// would then cache those blacked-out values as its "originals".
        /// </summary>
        private void RestoreLighting()
        {
            if (!_ambientCached || _blend <= 0f)
            {
                return;
            }

            _blend = 0f;
            RenderSettings.ambientLight = _originalAmbient;
            RenderSettings.ambientIntensity = _originalAmbientIntensity;

            for (int i = 0; i < _houseLights.Count; i++)
            {
                if (_houseLights[i] != null)
                {
                    _houseLights[i].intensity = _originalIntensities[i];
                }
            }
        }

        private void Update()
        {
            // A despawned handler keeps returning the last replicated IsActive, so without
            // this check the fade would hold the scene dark after the session has ended.
            if (!IsSpawned)
            {
                return;
            }

            // Runs on every peer: IsActive is replicated, so the fade is identical for all.
            float target = IsActive ? 1f : 0f;
            if (Mathf.Approximately(_blend, target))
            {
                return;
            }

            CacheAmbient();
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
