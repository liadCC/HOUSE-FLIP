using HouseFlip.Core;
using HouseFlip.GameFlow;
using HouseFlip.Networking;
using HouseFlip.Player;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

namespace HouseFlip.UI
{
    /// <summary>
    /// Create / join / ready-up (GDD 4). Direct IP for the MVP; the buttons stay the same
    /// when Steam lobbies arrive (GDD 26).
    /// </summary>
    public class LobbyUI : MonoBehaviour
    {
        [SerializeField] private GameObject root;
        [SerializeField] private InputField addressField;
        [SerializeField] private Text statusLabel;
        [SerializeField] private Text rosterLabel;
        [SerializeField] private Button hostButton;
        [SerializeField] private Button joinButton;
        [SerializeField] private Button readyButton;
        [SerializeField] private Button startButton;

        private void Awake()
        {
            if (root == null)
            {
                Build();
            }
        }

        private void OnEnable()
        {
            GameEvents.GameStateChanged += OnGameStateChanged;

            if (NetworkBootstrap.Exists)
            {
                NetworkBootstrap.Instance.StatusChanged += OnStatusChanged;
            }
        }

        private void OnDisable()
        {
            GameEvents.GameStateChanged -= OnGameStateChanged;

            if (NetworkBootstrap.Exists)
            {
                NetworkBootstrap.Instance.StatusChanged -= OnStatusChanged;
            }
        }

        private void Build()
        {
            Canvas canvas = UIFactory.EnsureCanvas("Screens_Canvas", 10);
            transform.SetParent(canvas.transform, false);

            Image panel = UIFactory.CreateFullScreenPanel(canvas.transform, "LobbyScreen",
                new Color(0.10f, 0.09f, 0.14f, 0.97f));
            root = panel.gameObject;

            UIFactory.CreateText(root.transform, "Title", "HOUSE FLIP", 96, TextAnchor.UpperCenter,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -70f), new Vector2(1200f, 120f)).color = UIFactory.Accent;

            UIFactory.CreateText(root.transform, "Tagline",
                "Buy a rundown house. Renovate it with friends.\nDon't destroy it completely.",
                30, TextAnchor.UpperCenter,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -190f), new Vector2(1200f, 90f));

            addressField = UIFactory.CreateInputField(root.transform, "Address", "127.0.0.1",
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, 130f), new Vector2(420f, 56f));

            hostButton = UIFactory.CreateButton(root.transform, "HOST GAME",
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(-230f, 40f), new Vector2(420f, 72f), OnHostClicked);

            joinButton = UIFactory.CreateButton(root.transform, "JOIN GAME",
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(230f, 40f), new Vector2(420f, 72f), OnJoinClicked);

            readyButton = UIFactory.CreateButton(root.transform, "READY",
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(-230f, -60f), new Vector2(420f, 72f), OnReadyClicked);

            startButton = UIFactory.CreateButton(root.transform, "START ROUND",
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(230f, -60f), new Vector2(420f, 72f), OnStartClicked);

            rosterLabel = UIFactory.CreateText(root.transform, "Roster", string.Empty, 28,
                TextAnchor.UpperCenter,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 1f),
                new Vector2(0f, -140f), new Vector2(800f, 200f));

            statusLabel = UIFactory.CreateText(root.transform, "Status", "Not connected", 26,
                TextAnchor.LowerCenter,
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 40f), new Vector2(1200f, 40f));
        }

        private void Update()
        {
            bool connected = NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening;

            SetInteractable(hostButton, !connected);
            SetInteractable(joinButton, !connected);
            SetInteractable(readyButton, connected);

            // Only the host can force the round to begin.
            bool isHost = NetworkManager.Singleton != null && NetworkManager.Singleton.IsHost;
            SetInteractable(startButton, connected && isHost);

            UpdateRoster();
        }

        private void UpdateRoster()
        {
            if (rosterLabel == null)
            {
                return;
            }

            if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening)
            {
                rosterLabel.text = string.Empty;
                return;
            }

            LobbyManager lobby = LobbyManager.Instance;
            var builder = new System.Text.StringBuilder();
            builder.AppendLine($"PLAYERS  {PlayerRegistry.Count} / 4");
            builder.AppendLine();

            foreach (var entry in PlayerRegistry.All)
            {
                PlayerController player = entry.Value;
                if (player == null)
                {
                    continue;
                }

                string name = player.Stats != null ? player.Stats.DisplayName.Value.ToString() : $"Player {entry.Key}";
                bool ready = lobby != null && lobby.IsReady(entry.Key);
                builder.AppendLine($"{name}   {(ready ? "READY" : "…")}");
            }

            rosterLabel.text = builder.ToString();
        }

        private void OnHostClicked()
        {
            if (!NetworkBootstrap.Exists)
            {
                return;
            }

            ApplyAddress();
            NetworkBootstrap.Instance.StartHost();
        }

        private void OnJoinClicked()
        {
            if (!NetworkBootstrap.Exists)
            {
                return;
            }

            ApplyAddress();
            NetworkBootstrap.Instance.StartClient();
        }

        private void ApplyAddress()
        {
            if (addressField == null)
            {
                return;
            }

            string value = addressField.text;
            ushort port = 7777;

            // Accept "host:port" as well as a bare address.
            int colon = value.LastIndexOf(':');
            if (colon > 0 && ushort.TryParse(value.Substring(colon + 1), out ushort parsed))
            {
                port = parsed;
                value = value.Substring(0, colon);
            }

            NetworkBootstrap.Instance.SetConnectionTarget(value, port);
        }

        private void OnReadyClicked() => LobbyManager.Instance?.ToggleReady();

        private void OnStartClicked() => LobbyManager.Instance?.ForceStart();

        private void OnStatusChanged(string status)
        {
            if (statusLabel != null)
            {
                statusLabel.text = status;
            }
        }

        private void OnGameStateChanged(GameState state)
        {
            if (root == null)
            {
                return;
            }

            bool inLobby = state == GameState.Lobby;
            root.SetActive(inLobby);

            if (!inLobby)
            {
                InspectionScreenUI.Hide();
                AwardsScreenUI.Hide();
                PlayerCameraRig.SetCursorLocked(true);
            }
        }

        private static void SetInteractable(Button button, bool interactable)
        {
            if (button != null)
            {
                button.interactable = interactable;
            }
        }
    }
}
