using Unity.Netcode;

namespace Teammate.Gameplay
{
    /// <summary>
    /// What this player is currently holding. Server-authoritative: every change comes
    /// from a station validating a ServerRpc (pickup, chop, cook, plate, deliver), never
    /// written directly by the owning client, so two players can't desync over the same
    /// item.
    /// </summary>
    [RequireComponent(typeof(NetworkObject))]
    public class PlayerCarrier : NetworkBehaviour
    {
        public NetworkVariable<CarriedItem> Carried = new NetworkVariable<CarriedItem>(
            default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        public bool IsEmpty => Carried.Value.IsEmpty;

        /// <summary>Server-side only: stations call this after validating an interaction.</summary>
        public void ServerSetCarried(CarriedItem item)
        {
            if (!IsServer) return;
            Carried.Value = item;
        }

        public void ServerClear() => ServerSetCarried(CarriedItem.Empty);
    }
}
