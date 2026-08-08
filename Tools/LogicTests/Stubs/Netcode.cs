// Stubs for Unity Netcode for GameObjects + Unity.Collections fixed strings.
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Unity.Collections
{
    public struct FixedString32Bytes
    {
        private string _v;
        public FixedString32Bytes(string s) { _v = s; }
        public int Length => _v == null ? 0 : _v.Length;
        public override string ToString() => _v ?? "";
        public static implicit operator FixedString32Bytes(string s) => new FixedString32Bytes(s);
    }

    public struct FixedString64Bytes
    {
        private string _v;
        public FixedString64Bytes(string s) { _v = s; }
        public int Length => _v == null ? 0 : _v.Length;
        public override string ToString() => _v ?? "";
        public static implicit operator FixedString64Bytes(string s) => new FixedString64Bytes(s);
    }
}

namespace Unity.Netcode
{
    public interface IReaderWriter { }

    public struct BufferSerializer<T> where T : IReaderWriter
    {
        public void SerializeValue(ref float v) { }
        public void SerializeValue(ref int v) { }
        public void SerializeValue(ref bool v) { }
        public void SerializeValue(ref ulong v) { }
        public void SerializeValue(ref string v) { }
        public void SerializeValue(ref Vector3 v) { }
        public void SerializeValue(ref Collections.FixedString32Bytes v) { }
        public void SerializeValue(ref Collections.FixedString64Bytes v) { }
    }

    public interface INetworkSerializable
    {
        void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter;
    }

    public enum NetworkVariableReadPermission { Everyone, Owner }
    public enum NetworkVariableWritePermission { Server, Owner }

    public abstract class NetworkVariableBase { }

    public class NetworkVariable<T> : NetworkVariableBase
    {
        public delegate void OnValueChangedDelegate(T previousValue, T newValue);

        public NetworkVariable() { }
        public NetworkVariable(T value) { Value = value; }
        public NetworkVariable(T value, NetworkVariableReadPermission read, NetworkVariableWritePermission write)
        { Value = value; }

        public T Value { get; set; }
        public event OnValueChangedDelegate OnValueChanged;
    }

    public struct NetworkListEvent<T>
    {
        public enum EventType { Add, Insert, Remove, RemoveAt, Value, Clear, Full }
        public EventType Type;
        public T Value;
        public int Index;
    }

    public class NetworkList<T> : NetworkVariableBase where T : unmanaged, IEquatable<T>
    {
        public delegate void OnListChangedDelegate(NetworkListEvent<T> changeEvent);

        public NetworkList() { }
        public int Count => 0;
        public T this[int index] { get => default; set { } }
        public void Add(T item) { }
        public void RemoveAt(int index) { }
        public bool Remove(T item) => false;
        public bool Contains(T item) => false;
        public void Clear() { }
        public event OnListChangedDelegate OnListChanged;
    }

    public class NetworkObject : UnityEngine.Component
    {
        public ulong NetworkObjectId => 0;
        public ulong OwnerClientId => 0;
        public bool IsSpawned => false;
        public void Spawn(bool destroyWithScene = false) { }
        public void Despawn(bool destroy = true) { }
    }

    public struct NetworkObjectReference : INetworkSerializable
    {
        public NetworkObjectReference(NetworkObject obj) { }
        public bool TryGet(out NetworkObject networkObject) { networkObject = null; return false; }
        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter { }
    }

    public struct ServerRpcReceiveParams { public ulong SenderClientId; }
    public struct ServerRpcSendParams { }
    public struct ServerRpcParams
    {
        public ServerRpcReceiveParams Receive;
        public ServerRpcSendParams Send;
    }

    public struct ClientRpcSendParams { public IReadOnlyList<ulong> TargetClientIds; }
    public struct ClientRpcReceiveParams { }
    public struct ClientRpcParams
    {
        public ClientRpcSendParams Send;
        public ClientRpcReceiveParams Receive;
    }

    [AttributeUsage(AttributeTargets.Method)]
    public class ServerRpcAttribute : Attribute { public bool RequireOwnership { get; set; } = true; }

    [AttributeUsage(AttributeTargets.Method)]
    public class ClientRpcAttribute : Attribute { }

    public abstract class NetworkBehaviour : UnityEngine.MonoBehaviour
    {
        public bool IsServer => false;
        public bool IsClient => false;
        public bool IsHost => false;
        public bool IsOwner => false;
        public bool IsLocalPlayer => false;
        public bool IsSpawned => false;
        public ulong OwnerClientId => 0;
        public NetworkObject NetworkObject => null;
        public NetworkManager NetworkManager => null;

        public virtual void OnNetworkSpawn() { }
        public virtual void OnNetworkDespawn() { }
        public virtual void OnDestroy() { }
    }

    public class NetworkPrefab { public GameObject Prefab; }

    public class NetworkPrefabs
    {
        public void Add(NetworkPrefab p) { }
    }

    public class NetworkConfig
    {
        public NetworkTransport NetworkTransport;
        public GameObject PlayerPrefab;
        public bool ConnectionApproval;
        public bool EnableSceneManagement = true;
        public int TickRate;
        public NetworkPrefabs Prefabs = new NetworkPrefabs();
    }

    public abstract class NetworkTransport : UnityEngine.Component { }

    public class NetworkSpawnManager
    {
        public IReadOnlyDictionary<ulong, NetworkObject> SpawnedObjects =>
            new Dictionary<ulong, NetworkObject>();
        public NetworkObject GetPlayerNetworkObject(ulong clientId) => null;
    }

    public class NetworkManager : UnityEngine.MonoBehaviour
    {
        public struct ConnectionApprovalRequest { public ulong ClientNetworkId; public byte[] Payload; }
        public class ConnectionApprovalResponse
        {
            public bool Approved;
            public bool CreatePlayerObject;
            public bool Pending;
            public string Reason;
        }

        public static NetworkManager Singleton => null;
        public const ulong ServerClientId = 0;

        public bool IsServer => false;
        public bool IsClient => false;
        public bool IsHost => false;
        public bool IsListening => false;
        public ulong LocalClientId => 0;
        public string DisconnectReason => "";
        public NetworkConfig NetworkConfig { get; set; }
        public NetworkSpawnManager SpawnManager => null;
        public IReadOnlyList<ulong> ConnectedClientsIds => new List<ulong>();

        public Action<ConnectionApprovalRequest, ConnectionApprovalResponse> ConnectionApprovalCallback { get; set; }
        public event Action<ulong> OnClientConnectedCallback;
        public event Action<ulong> OnClientDisconnectCallback;
        public event Action OnServerStarted;

        public bool StartHost() => false;
        public bool StartClient() => false;
        public bool StartServer() => false;
        public void Shutdown() { }
        public void AddNetworkPrefab(GameObject prefab) { }
    }
}

namespace Unity.Netcode.Components
{
    public class NetworkTransform : NetworkBehaviour
    {
        protected virtual bool OnIsServerAuthoritative() => true;
    }

    public class NetworkRigidbody : NetworkBehaviour { }
}

namespace Unity.Netcode.Transports.UTP
{
    public class UnityTransport : NetworkTransport
    {
        public void SetConnectionData(string address, ushort port) { }
        public void SetConnectionData(string address, ushort port, string listenAddress) { }
    }
}
