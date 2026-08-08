using System.Collections.Generic;
using HouseFlip.Building;
using HouseFlip.Cleaning;
using HouseFlip.Core;
using HouseFlip.Demolition;
using HouseFlip.Economy;
using HouseFlip.Furniture;
using HouseFlip.Painting;
using HouseFlip.Player;
using HouseFlip.Polish;
using HouseFlip.Repair;
using Unity.Netcode;
using UnityEngine;

namespace HouseFlip.Networking
{
    /// <summary>
    /// Every action that mutates shared house state funnels through here (GDD 22).
    ///
    /// One gateway rather than an RPC per component, because the validation is the same
    /// every time: is the sender close enough, holding the right tool, and can the group
    /// afford it? Doing that in one place means a new interactable cannot forget a check.
    /// </summary>
    public class RenovationService : NetSingleton<RenovationService>
    {
        [SerializeField] private PlacementCatalog catalog;

        [Tooltip("Seconds between hammer swings from the same player.")]
        [SerializeField] private float swingCooldown = 0.45f;

        [Tooltip("Server-side clamp on client-reported delta time, to stop a hacked client from cleaning instantly.")]
        [SerializeField] private float maxReportedDelta = 0.1f;

        /// <summary>Extra slack on the server range check to absorb latency.</summary>
        private const float RangeTolerance = ActorValidator.RangeTolerance;

        private readonly Dictionary<ulong, float> _lastSwingTime = new Dictionary<ulong, float>();

        public PlacementCatalog Catalog => catalog;

        // ==================================================================
        // Demolition (GDD 9)
        // ==================================================================

        public void RequestDemolishHit(Destructible target)
        {
            if (target != null)
            {
                DemolishServerRpc(new NetworkObjectReference(target.NetworkObject));
            }
        }

        [ServerRpc(RequireOwnership = false)]
        private void DemolishServerRpc(NetworkObjectReference reference, ServerRpcParams rpcParams = default)
        {
            ulong sender = rpcParams.Receive.SenderClientId;

            if (!TryResolve(reference, out Destructible destructible))
            {
                return;
            }

            if (!ValidateActor(sender, destructible.transform.position, ToolType.Hammer, out PlayerController _))
            {
                return;
            }

            if (!ConsumeSwing(sender))
            {
                return;
            }

            destructible.ServerApplyHit(sender);
        }

        private bool ConsumeSwing(ulong clientId)
        {
            float now = Time.time;
            if (_lastSwingTime.TryGetValue(clientId, out float last) && now - last < swingCooldown)
            {
                return false;
            }

            _lastSwingTime[clientId] = now;
            return true;
        }

        // ==================================================================
        // Cleaning (GDD 12)
        // ==================================================================

        public void RequestClean(DirtSource target, float amount)
        {
            if (target != null && amount > 0f)
            {
                CleanServerRpc(new NetworkObjectReference(target.NetworkObject), amount);
            }
        }

        [ServerRpc(RequireOwnership = false)]
        private void CleanServerRpc(NetworkObjectReference reference, float amount, ServerRpcParams rpcParams = default)
        {
            ulong sender = rpcParams.Receive.SenderClientId;

            if (!TryResolve(reference, out DirtSource dirt))
            {
                return;
            }

            if (!ValidateActor(sender, dirt.transform.position, ToolType.CleaningTool, out PlayerController _))
            {
                return;
            }

            dirt.ServerClean(sender, Mathf.Clamp(amount, 0f, maxReportedDelta));
        }

        // ==================================================================
        // Repair (GDD 13)
        // ==================================================================

        public void RequestRepair(RepairableFixture target, float deltaTime)
        {
            if (target != null && deltaTime > 0f)
            {
                RepairServerRpc(new NetworkObjectReference(target.NetworkObject), deltaTime);
            }
        }

        [ServerRpc(RequireOwnership = false)]
        private void RepairServerRpc(NetworkObjectReference reference, float deltaTime, ServerRpcParams rpcParams = default)
        {
            ulong sender = rpcParams.Receive.SenderClientId;

            if (!TryResolve(reference, out RepairableFixture fixture))
            {
                return;
            }

            if (!ValidateActor(sender, fixture.transform.position, fixture.RequiredTool, out PlayerController _))
            {
                return;
            }

            fixture.ServerAdvanceRepair(sender, Mathf.Clamp(deltaTime, 0f, maxReportedDelta));
        }

        // ==================================================================
        // Painting (GDD 14)
        // ==================================================================

        public void RequestPaint(PaintableWall wall, int colorIndex)
        {
            if (wall != null)
            {
                PaintServerRpc(new NetworkObjectReference(wall.NetworkObject), colorIndex);
            }
        }

        [ServerRpc(RequireOwnership = false)]
        private void PaintServerRpc(NetworkObjectReference reference, int colorIndex, ServerRpcParams rpcParams = default)
        {
            ulong sender = rpcParams.Receive.SenderClientId;

            if (!TryResolve(reference, out PaintableWall wall))
            {
                return;
            }

            if (!ValidateActor(sender, wall.transform.position, ToolType.PaintRoller, out PlayerController _))
            {
                return;
            }

            wall.ServerPaint(sender, colorIndex);
        }

        // ==================================================================
        // Building and furniture placement (GDD 10, 11)
        // ==================================================================

        public void RequestPlace(CatalogKind kind, int index, Vector3 position, Quaternion rotation)
        {
            PlaceServerRpc((int)kind, index, position, rotation);
        }

        [ServerRpc(RequireOwnership = false)]
        private void PlaceServerRpc(int kindValue, int index, Vector3 position, Quaternion rotation,
            ServerRpcParams rpcParams = default)
        {
            ulong sender = rpcParams.Receive.SenderClientId;

            if (catalog == null)
            {
                Debug.LogError("[RenovationService] No PlacementCatalog assigned — placement is disabled.");
                return;
            }

            var kind = (CatalogKind)kindValue;
            PlaceableData data = catalog.Resolve(kind, index);
            if (data == null || data.prefab == null)
            {
                return;
            }

            PlayerController player = PlayerRegistry.Get(sender);
            if (player == null)
            {
                return;
            }

            // Same distance rule the client previewed against, plus latency slack.
            if (Vector3.Distance(player.transform.position, position) > 6f + RangeTolerance)
            {
                return;
            }

            if (!PlacementValidator.IsValidServerPlacement(data, position, rotation))
            {
                return;
            }

            BudgetManager budget = BudgetManager.Instance;
            SpendCategory category = data.IsFurniture ? SpendCategory.Furniture : SpendCategory.Renovation;

            if (budget != null && !budget.TrySpend(data.cost, sender, category))
            {
                return;
            }

            SpawnPlaceable(data, position, rotation, sender);
        }

        private void SpawnPlaceable(PlaceableData data, Vector3 position, Quaternion rotation, ulong sender)
        {
            GameObject instance = Instantiate(data.prefab, position, rotation);

            var networkObject = instance.GetComponent<NetworkObject>();
            if (networkObject == null)
            {
                Debug.LogError($"[RenovationService] Prefab '{data.prefab.name}' has no NetworkObject — cannot spawn.");
                Destroy(instance);
                BudgetManager.Instance?.Refund(data.cost,
                    data.IsFurniture ? SpendCategory.Furniture : SpendCategory.Renovation);
                return;
            }

            networkObject.Spawn(true);

            var placed = instance.GetComponent<PlacedFurniture>();
            if (placed == null)
            {
                placed = instance.AddComponent<PlacedFurniture>();
            }

            RoomController room = RoomRegistry.FindRoom(position);
            placed.ServerInitialise(data, room);

            PlayerStatsTracker.Record(sender, PlayerStat.FurniturePlaced, 1f);
            PlayerStatsTracker.Record(sender, PlayerStat.HouseValueAdded, data.valueContribution);
            PlayerStatsTracker.Record(sender, PlayerStat.DesignPointsAdded, data.designPoints);

            PlacedClientRpc(position);
        }

        [ClientRpc]
        private void PlacedClientRpc(Vector3 position)
        {
            GameEvents.RaiseSfx(SfxId.BuildComplete);

            // A small thump so a placement lands rather than silently appearing.
            ImpactFeedback.Shake(position, 0.12f);
        }

        // ==================================================================
        // Selling furniture back
        // ==================================================================

        public void RequestSellFurniture(PlacedFurniture target)
        {
            if (target != null)
            {
                SellServerRpc(new NetworkObjectReference(target.NetworkObject));
            }
        }

        [ServerRpc(RequireOwnership = false)]
        private void SellServerRpc(NetworkObjectReference reference, ServerRpcParams rpcParams = default)
        {
            ulong sender = rpcParams.Receive.SenderClientId;

            if (!TryResolve(reference, out PlacedFurniture placed))
            {
                return;
            }

            // Range only: selling is triggered while carrying the item, and the player may
            // well still have a tool equipped from whatever they were doing before.
            PlayerController seller = PlayerRegistry.Get(sender);
            if (seller == null ||
                Vector3.Distance(seller.transform.position, placed.transform.position)
                > GameConstants.InteractRange + RangeTolerance)
            {
                return;
            }

            placed.ServerSellBack(sender);
        }

        // ==================================================================
        // Shared validation
        // ==================================================================

        private static bool TryResolve<T>(NetworkObjectReference reference, out T component) where T : Component
        {
            component = null;

            if (!reference.TryGet(out NetworkObject networkObject) || networkObject == null)
            {
                return false;
            }

            component = networkObject.GetComponent<T>();
            return component != null;
        }

        /// <summary>
        /// Confirms the sender exists, is standing near the target, and is holding the
        /// tool the action requires. Everything the client claimed is re-derived here.
        /// </summary>
        private static bool ValidateActor(ulong clientId, Vector3 targetPosition, ToolType requiredTool,
            out PlayerController player)
        {
            return ActorValidator.Validate(clientId, targetPosition, requiredTool, out player);
        }
    }
}
