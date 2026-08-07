using Unity.Netcode;
using UnityEngine;

namespace HouseFlip.Core
{
    /// <summary>
    /// Base class for the host-authoritative managers described in GDD 25
    /// (BudgetManager, HouseValueManager, TimerManager, GameManager).
    ///
    /// These live on a single "Systems" NetworkObject in the scene. Clients may read
    /// <see cref="Instance"/> but must never write shared state directly — writes go
    /// through ServerRpcs so there is exactly one source of truth (GDD 22).
    /// </summary>
    public abstract class NetSingleton<T> : NetworkBehaviour where T : NetSingleton<T>
    {
        public static T Instance { get; private set; }

        public static bool Exists => Instance != null;

        protected virtual void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning($"[{typeof(T).Name}] duplicate instance on '{name}' — destroying the newcomer.");
                Destroy(gameObject);
                return;
            }

            Instance = (T)this;
        }

        public override void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }

            base.OnDestroy();
        }
    }

    /// <summary>Plain (non-networked) singleton for purely local services such as audio and HUD.</summary>
    public abstract class LocalSingleton<T> : MonoBehaviour where T : LocalSingleton<T>
    {
        public static T Instance { get; private set; }

        public static bool Exists => Instance != null;

        protected virtual void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = (T)this;
        }

        protected virtual void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }
    }
}
