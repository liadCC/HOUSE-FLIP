using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace HouseFlip.Networking
{
    /// <summary>
    /// Registers every dynamically spawned prefab with the NetworkManager at runtime.
    ///
    /// Uses the runtime <c>AddNetworkPrefab</c> API rather than the inspector list so the
    /// scene generator can wire prefabs up without depending on Netcode's serialized
    /// prefab-list format, which has changed between major versions.
    /// </summary>
    [RequireComponent(typeof(NetworkManager))]
    public class NetworkPrefabRegistrar : MonoBehaviour
    {
        [Tooltip("Every prefab the game spawns at runtime: furniture, build pieces, rubble.")]
        [SerializeField] private List<GameObject> prefabs = new List<GameObject>();

        private void Awake()
        {
            NetworkManager manager = GetComponent<NetworkManager>();
            if (manager == null)
            {
                return;
            }

            foreach (GameObject prefab in prefabs)
            {
                if (prefab == null)
                {
                    continue;
                }

                if (prefab.GetComponent<NetworkObject>() == null)
                {
                    Debug.LogWarning($"[NetworkPrefabRegistrar] '{prefab.name}' has no NetworkObject — skipped.");
                    continue;
                }

                // Netcode logs its own warning on a duplicate, which is harmless but noisy
                // when a prefab also appears in the inspector list.
                manager.AddNetworkPrefab(prefab);
            }
        }

        public void SetPrefabs(List<GameObject> value) => prefabs = value;
    }
}
