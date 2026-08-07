using HouseFlip.Core;
using HouseFlip.Economy;
using HouseFlip.Networking;
using HouseFlip.Player;
using HouseFlip.UI;
using UnityEngine;

namespace HouseFlip.Building
{
    /// <summary>
    /// Client-side placement mode (GDD 10, 11): a ghost mesh follows the player's aim,
    /// snaps to the grid, and turns green or red. Confirming sends one RPC — the server
    /// re-runs every check before it spends a cent or spawns anything.
    ///
    /// Local-only by design. Nothing here is trusted.
    /// </summary>
    public class PlacementController : MonoBehaviour
    {
        [SerializeField] private PlacementCatalog catalog;
        [SerializeField] private Material ghostMaterial;
        [SerializeField] private Color validColor = new Color(0.35f, 1f, 0.45f, 0.45f);
        [SerializeField] private Color invalidColor = new Color(1f, 0.32f, 0.28f, 0.45f);
        [SerializeField] private float maxPlacementDistance = 6f;

        private PlayerController _player;
        private Camera _camera;

        private GameObject _ghost;
        private Renderer[] _ghostRenderers;
        private MaterialPropertyBlock _block;

        private PlaceableData _selected;
        private CatalogKind _selectedKind;
        private int _selectedIndex = -1;
        private float _yaw;
        private bool _valid;

        public bool IsPlacing => _selected != null;
        public PlacementCatalog Catalog => catalog;

        private void Awake()
        {
            _player = GetComponent<PlayerController>();
            _block = new MaterialPropertyBlock();

            if (catalog == null && RenovationService.Instance != null)
            {
                catalog = RenovationService.Instance.Catalog;
            }
        }

        private void OnDisable() => Cancel();

        /// <summary>Enter placement mode for a catalog entry.</summary>
        public void BeginPlacement(CatalogKind kind, int index)
        {
            if (catalog == null)
            {
                catalog = RenovationService.Instance != null ? RenovationService.Instance.Catalog : null;
            }

            PlaceableData data = catalog != null ? catalog.Resolve(kind, index) : null;
            if (data == null || data.prefab == null)
            {
                return;
            }

            Cancel();

            _selected = data;
            _selectedKind = kind;
            _selectedIndex = index;
            _yaw = transform.eulerAngles.y;

            BuildGhost(data);
        }

        public void Cancel()
        {
            bool wasPlacing = _selected != null;

            if (_ghost != null)
            {
                Destroy(_ghost);
                _ghost = null;
            }

            _ghostRenderers = null;
            _selected = null;
            _selectedIndex = -1;
            _valid = false;

            // The placement hint is an override, so leaving it set would pin stale text
            // to the middle of the screen for the rest of the round.
            if (wasPlacing)
            {
                InteractionPromptUI.SetOverride(null);
            }
        }

        private void Update()
        {
            if (_player == null || !_player.IsOwner || !IsPlacing)
            {
                return;
            }

            if (!PlayerController.InputEnabled)
            {
                Cancel();
                return;
            }

            if (_camera == null)
            {
                _camera = Camera.main;
                if (_camera == null)
                {
                    return;
                }
            }

            if (Input.GetKeyDown(KeyCode.R))
            {
                _yaw += 90f;
            }

            if (Input.GetKeyDown(KeyCode.Escape) || Input.GetMouseButtonDown(1))
            {
                Cancel();
                return;
            }

            UpdateGhost();

            if (_valid && (Input.GetMouseButtonDown(0) || Input.GetKeyDown(KeyCode.E)))
            {
                Confirm();
            }
        }

        private void UpdateGhost()
        {
            if (_ghost == null)
            {
                return;
            }

            Ray ray = new Ray(_camera.transform.position, _camera.transform.forward);

            if (!Physics.Raycast(ray, out RaycastHit hit, maxPlacementDistance + 4f,
                    GameLayers.PlacementSurfaceMask, QueryTriggerInteraction.Ignore))
            {
                SetGhostVisible(false);
                _valid = false;
                return;
            }

            SetGhostVisible(true);

            Vector3 snapped = GridUtility.SnapFootprint(hit.point, _selected.size);
            Quaternion rotation = GridUtility.SnapRotation(_yaw);

            _ghost.transform.SetPositionAndRotation(snapped, rotation);

            _valid = Validate(snapped, rotation, hit, out string reason);
            ApplyGhostColor(_valid ? validColor : invalidColor);

            InteractionPromptUI.SetOverride(_valid
                ? $"[LMB] Place {_selected.itemName} (${_selected.cost:0})  •  [R] Rotate  •  [RMB] Cancel"
                : reason);
        }

        /// <summary>
        /// Local validation. Mirrored server-side in <see cref="PlacementValidator"/> so the
        /// preview and the authoritative check never disagree.
        /// </summary>
        private bool Validate(Vector3 position, Quaternion rotation, RaycastHit surface, out string reason)
        {
            reason = null;

            if (Vector3.Distance(transform.position, position) > maxPlacementDistance)
            {
                reason = "Too Far";
                return false;
            }

            if (BudgetManager.Exists && !BudgetManager.Instance.CanAfford(_selected.cost))
            {
                reason = "No Budget!";
                return false;
            }

            if (_selected is BuildingData building && building.requiresFloorContact
                && Vector3.Dot(surface.normal, Vector3.up) < 0.5f)
            {
                reason = "Needs Floor";
                return false;
            }

            if (PlacementValidator.IsBlocked(_selected, position, rotation, _ghost))
            {
                reason = "Blocked";
                return false;
            }

            return true;
        }

        private void Confirm()
        {
            RenovationService service = RenovationService.Instance;
            if (service == null || _ghost == null)
            {
                return;
            }

            service.RequestPlace(_selectedKind, _selectedIndex, _ghost.transform.position, _ghost.transform.rotation);

            // Shift-place keeps the ghost up for rapid tiling; a plain click exits.
            if (!Input.GetKey(KeyCode.LeftShift))
            {
                Cancel();
            }
        }

        // ------------------------------------------------------------------
        // Ghost construction
        // ------------------------------------------------------------------

        private void BuildGhost(PlaceableData data)
        {
            _ghost = Instantiate(data.prefab);
            _ghost.name = $"Ghost_{data.itemName}";

            StripForPreview(_ghost);

            _ghostRenderers = _ghost.GetComponentsInChildren<Renderer>(true);

            Material material = ghostMaterial != null ? ghostMaterial : CreateFallbackGhostMaterial();
            foreach (Renderer renderer in _ghostRenderers)
            {
                var materials = new Material[renderer.sharedMaterials.Length];
                for (int i = 0; i < materials.Length; i++)
                {
                    materials[i] = material;
                }

                renderer.sharedMaterials = materials;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }

            SetLayerRecursive(_ghost.transform, GameLayers.PlacementGhost);
        }

        /// <summary>A preview must not collide, network, or run gameplay logic.</summary>
        private static void StripForPreview(GameObject ghost)
        {
            foreach (Collider collider in ghost.GetComponentsInChildren<Collider>(true))
            {
                collider.enabled = false;
            }

            foreach (Rigidbody body in ghost.GetComponentsInChildren<Rigidbody>(true))
            {
                body.isKinematic = true;
                body.detectCollisions = false;
            }

            foreach (MonoBehaviour behaviour in ghost.GetComponentsInChildren<MonoBehaviour>(true))
            {
                behaviour.enabled = false;
            }

            foreach (Unity.Netcode.NetworkObject networkObject in
                     ghost.GetComponentsInChildren<Unity.Netcode.NetworkObject>(true))
            {
                Destroy(networkObject);
            }
        }

        private static Material CreateFallbackGhostMaterial()
        {
            Shader shader = Shader.Find("Standard");
            var material = new Material(shader != null ? shader : Shader.Find("Diffuse"));

            // Standard shader transparency needs these flags set explicitly at runtime.
            material.SetFloat("_Mode", 3f);
            material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            material.SetInt("_ZWrite", 0);
            material.DisableKeyword("_ALPHATEST_ON");
            material.EnableKeyword("_ALPHABLEND_ON");
            material.renderQueue = 3000;

            return material;
        }

        private void ApplyGhostColor(Color color)
        {
            if (_ghostRenderers == null)
            {
                return;
            }

            foreach (Renderer renderer in _ghostRenderers)
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

        private void SetGhostVisible(bool visible)
        {
            if (_ghostRenderers == null)
            {
                return;
            }

            foreach (Renderer renderer in _ghostRenderers)
            {
                if (renderer != null)
                {
                    renderer.enabled = visible;
                }
            }
        }

        private static void SetLayerRecursive(Transform root, int layer)
        {
            root.gameObject.layer = layer;
            for (int i = 0; i < root.childCount; i++)
            {
                SetLayerRecursive(root.GetChild(i), layer);
            }
        }
    }
}
