using System.Collections.Generic;
using HouseFlip.Core;
using HouseFlip.Economy;
using HouseFlip.Interaction;
using HouseFlip.Networking;
using HouseFlip.Player;
using Unity.Netcode;
using UnityEngine;

namespace HouseFlip.Painting
{
    /// <summary>
    /// A paintable wall surface (GDD 14). Flat fee per wall, adds design points, and
    /// grants a bonus when the room ends up in a coherent colour scheme.
    /// </summary>
    public class PaintableWall : NetworkBehaviour, IInteractable, IToolGated
    {
        [SerializeField] private Renderer targetRenderer;
        [SerializeField] private float paintCost = GameConstants.PaintCostPerWall;

        private readonly NetworkVariable<int> _colorIndex = new NetworkVariable<int>(
            PaintColors.Unpainted, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        private RoomController _room;
        private MaterialPropertyBlock _block;

        /// <summary>Every wall in the scene, so the room can be checked for a matching scheme.</summary>
        private static readonly List<PaintableWall> AllWalls = new List<PaintableWall>();

        public ToolType RequiredTool => ToolType.PaintRoller;

        public int CurrentColorIndex => _colorIndex.Value;
        public Color CurrentColor => PaintColors.Get(_colorIndex.Value);
        public RoomController Room => _room;

        private void Awake()
        {
            if (targetRenderer == null)
            {
                targetRenderer = GetComponentInChildren<Renderer>();
            }

            _block = new MaterialPropertyBlock();
            AllWalls.Add(this);
        }

        private void Start()
        {
            _room = RoomRegistry.FindRoom(transform.position);
        }

        public override void OnDestroy()
        {
            AllWalls.Remove(this);
            base.OnDestroy();
        }

        public override void OnNetworkSpawn()
        {
            _colorIndex.OnValueChanged += OnColorIndexChanged;
            ApplyColor(_colorIndex.Value);
        }

        public override void OnNetworkDespawn()
        {
            _colorIndex.OnValueChanged -= OnColorIndexChanged;
        }

        public string GetPromptText()
        {
            PlayerController local = PlayerRegistry.LocalPlayer;
            if (local == null || local.Tools == null)
            {
                return null;
            }

            if (local.Tools.CurrentTool != ToolType.PaintRoller)
            {
                return null;
            }

            int selected = local.Tools.PaintColorIndex;
            if (selected == _colorIndex.Value)
            {
                return $"Already {PaintColors.GetName(selected)}";
            }

            return $"Paint {PaintColors.GetName(selected)} (${paintCost:0})";
        }

        public void OnInteract(PlayerController player)
        {
            if (player == null || player.Tools == null || player.Tools.CurrentTool != ToolType.PaintRoller)
            {
                return;
            }

            int selected = player.Tools.PaintColorIndex;
            if (selected == _colorIndex.Value)
            {
                return;
            }

            RenovationService.Instance?.RequestPaint(this, selected);
        }

        /// <summary>Server only. Charges the budget and repaints (GDD 14).</summary>
        public void ServerPaint(ulong painterClientId, int colorIndex)
        {
            if (!IsServer || colorIndex < 0 || colorIndex >= PaintColors.Count)
            {
                return;
            }

            if (colorIndex == _colorIndex.Value)
            {
                return;
            }

            BudgetManager budget = BudgetManager.Instance;
            if (budget != null && !budget.TrySpend(paintCost, painterClientId, SpendCategory.Renovation))
            {
                return;
            }

            _colorIndex.Value = colorIndex;

            float points = GameConstants.PaintDesignPoints;
            if (RoomSchemeIsHarmonious())
            {
                points += GameConstants.MatchingPaletteBonusPoints;
            }

            if (_room != null)
            {
                _room.AddDesignPoints(points);
            }

            PlayerStatsTracker.Record(painterClientId, PlayerStat.WallsPainted, 1f);
            PlayerStatsTracker.Record(painterClientId, PlayerStat.DesignPointsAdded, points);

            GameEvents.RaiseHouseStateDirty();
            PaintedClientRpc();
        }

        /// <summary>True when every painted wall in this room sits in a matching scheme.</summary>
        /// <summary>
        /// Server only. Strips the paint back to bare plaster for a new round.
        ///
        /// Not a call to ServerPaint: that charges the budget, and it early-returns when
        /// the colour is unchanged — so without a separate path a round-2 team had to pick
        /// a *different* colour than round 1 to earn the points for the same work.
        /// </summary>
        public void ServerResetForNewRound()
        {
            if (IsServer)
            {
                _colorIndex.Value = PaintColors.Unpainted;
            }
        }

        /// <summary>Every wall currently standing in the given room. Server-side helper for the round reset.</summary>
        public static void ServerResetRoom(RoomController room)
        {
            foreach (PaintableWall wall in AllWalls)
            {
                if (wall != null && wall.Room == room)
                {
                    wall.ServerResetForNewRound();
                }
            }
        }

        private bool RoomSchemeIsHarmonious()
        {
            if (_room == null)
            {
                return false;
            }

            int painted = 0;

            // Every painted pair has to match, not just each wall against the one that was
            // painted last. IsHarmonious is not transitive — white goes with anything — so
            // checking only against the new colour meant a room already holding a clashing
            // Blue and Red started paying the matching-palette bonus the moment somebody
            // added a white wall.
            for (int i = 0; i < AllWalls.Count; i++)
            {
                PaintableWall wall = AllWalls[i];
                if (wall == null || wall._room != _room || wall._colorIndex.Value < 0)
                {
                    continue;
                }

                painted++;

                for (int j = i + 1; j < AllWalls.Count; j++)
                {
                    PaintableWall other = AllWalls[j];
                    if (other == null || other._room != _room || other._colorIndex.Value < 0)
                    {
                        continue;
                    }

                    if (!PaintColors.IsHarmonious(wall._colorIndex.Value, other._colorIndex.Value))
                    {
                        return false;
                    }
                }
            }

            // A single painted wall is not yet a colour scheme.
            return painted >= 2;
        }

        private void OnColorIndexChanged(int previous, int current) => ApplyColor(current);

        private void ApplyColor(int colorIndex)
        {
            if (targetRenderer == null)
            {
                return;
            }

            Color color = PaintColors.Get(colorIndex);
            targetRenderer.GetPropertyBlock(_block);
            _block.SetColor("_Color", color);
            _block.SetColor("_BaseColor", color);
            targetRenderer.SetPropertyBlock(_block);
        }

        [ClientRpc]
        private void PaintedClientRpc() => GameEvents.RaiseSfx(SfxId.Paint);
    }
}
