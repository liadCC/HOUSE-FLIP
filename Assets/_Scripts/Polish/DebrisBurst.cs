using System.Collections.Generic;
using UnityEngine;

namespace HouseFlip.Polish
{
    /// <summary>
    /// Chunky physical debris on destruction (GDD 9, GDD 18 — Polish).
    ///
    /// Deliberately real rigidbodies rather than a particle system: in a game whose whole
    /// appeal is physics comedy, debris that bounces off a player and skitters under the
    /// sofa is worth more than a prettier but inert puff. The pieces are cosmetic and
    /// purely local — they are never networked, so four players each simulating their own
    /// rubble costs nothing in bandwidth.
    /// </summary>
    public class DebrisBurst : MonoBehaviour
    {
        private static readonly Queue<GameObject> Pool = new Queue<GameObject>();
        private static Transform _poolRoot;
        private static Material _sharedMaterial;

        private const int PoolLimit = 120;

        /// <summary>
        /// Throws a handful of chunks out from a point.
        /// Safe to call on any client; does nothing on a dedicated server with no camera.
        /// </summary>
        public static void Spawn(Vector3 position, Color color, int pieces = 8, float force = 3.5f,
            float size = 0.16f, float lifetime = 4f)
        {
            if (Camera.main == null)
            {
                return;
            }

            EnsureRoot();

            pieces = Mathf.Clamp(pieces, 1, 24);

            for (int i = 0; i < pieces; i++)
            {
                GameObject chunk = Rent();
                if (chunk == null)
                {
                    return;
                }

                float scale = size * Random.Range(0.6f, 1.4f);
                chunk.transform.localScale = new Vector3(scale, scale, scale);
                chunk.transform.position = position + Random.insideUnitSphere * 0.25f;
                chunk.transform.rotation = Quaternion.Euler(
                    Random.Range(0f, 360f), Random.Range(0f, 360f), Random.Range(0f, 360f));

                var renderer = chunk.GetComponent<Renderer>();
                if (renderer != null)
                {
                    var block = new MaterialPropertyBlock();
                    Color tint = color * Random.Range(0.75f, 1.15f);
                    block.SetColor("_Color", tint);
                    block.SetColor("_BaseColor", tint);
                    renderer.SetPropertyBlock(block);
                }

                var body = chunk.GetComponent<Rigidbody>();
                if (body != null)
                {
                    body.linearVelocity = Vector3.zero;
                    body.angularVelocity = Vector3.zero;

                    Vector3 direction = (Random.insideUnitSphere + Vector3.up * 0.8f).normalized;
                    body.AddForce(direction * force * Random.Range(0.7f, 1.3f), ForceMode.VelocityChange);
                    body.AddTorque(Random.insideUnitSphere * 6f, ForceMode.VelocityChange);
                }

                chunk.SetActive(true);

                var recycler = chunk.GetComponent<DebrisBurst>();
                recycler.Begin(lifetime);
            }
        }

        private static void EnsureRoot()
        {
            if (_poolRoot == null)
            {
                _poolRoot = new GameObject("~DebrisPool").transform;
            }

            if (_sharedMaterial == null)
            {
                Shader shader = Shader.Find("Standard") ?? Shader.Find("Diffuse");
                if (shader != null)
                {
                    _sharedMaterial = new Material(shader);
                }
            }
        }

        private static GameObject Rent()
        {
            if (Pool.Count > 0)
            {
                return Pool.Dequeue();
            }

            GameObject chunk = GameObject.CreatePrimitive(PrimitiveType.Cube);
            chunk.name = "DebrisChunk";
            chunk.transform.SetParent(_poolRoot, false);

            var renderer = chunk.GetComponent<Renderer>();
            if (renderer != null && _sharedMaterial != null)
            {
                renderer.sharedMaterial = _sharedMaterial;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }

            var body = chunk.AddComponent<Rigidbody>();
            body.mass = 0.2f;
            body.interpolation = RigidbodyInterpolation.Interpolate;

            // Debris must never shove a player or a carried sofa around — it is decoration
            // that happens to fall convincingly.
            chunk.layer = Core.GameLayers.IgnoreRaycast;

            chunk.AddComponent<DebrisBurst>();
            chunk.SetActive(false);

            return chunk;
        }

        private float _remaining;
        private bool _active;

        private void Begin(float lifetime)
        {
            _remaining = lifetime;
            _active = true;
        }

        private void Update()
        {
            if (!_active)
            {
                return;
            }

            _remaining -= Time.deltaTime;
            if (_remaining > 0f)
            {
                return;
            }

            _active = false;
            gameObject.SetActive(false);

            // Drop the chunk instead of pooling it once the pool is saturated, so a very
            // long demolition spree cannot grow the pool without bound.
            if (Pool.Count < PoolLimit)
            {
                Pool.Enqueue(gameObject);
            }
            else
            {
                Destroy(gameObject);
            }
        }
    }
}
