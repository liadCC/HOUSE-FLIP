using Unity.Netcode.Components;
using UnityEngine;

namespace HouseFlip.Networking
{
    /// <summary>
    /// Owner-authoritative transform for player characters (GDD 22: "Clients are
    /// authoritative over their own character position only").
    ///
    /// Everything else in the house keeps the default server-authoritative
    /// <see cref="NetworkTransform"/>, so shared objects cannot be moved by a client
    /// that simply decides they should be somewhere else.
    /// </summary>
    [DisallowMultipleComponent]
    public class ClientNetworkTransform : NetworkTransform
    {
        protected override bool OnIsServerAuthoritative() => false;
    }
}
