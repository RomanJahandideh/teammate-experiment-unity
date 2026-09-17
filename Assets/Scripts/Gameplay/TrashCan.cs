using Unity.Netcode;
using UnityEngine;

namespace Teammate.Gameplay
{
    /// <summary>Discards whatever the interacting player is holding — burned ingredients, wrong dishes, etc.</summary>
    [RequireComponent(typeof(NetworkObject))]
    public class TrashCan : NetworkBehaviour, IInteractableStation
    {
        public string InteractPrompt => "Discard";

        public void ClientRequestInteract() => RequestDiscardServerRpc();

        [ServerRpc(RequireOwnership = false)]
        private void RequestDiscardServerRpc(ServerRpcParams rpcParams = default)
        {
            var carrier = ServerPlayerLookup.GetCarrier(rpcParams.Receive.SenderClientId);
            if (carrier == null || carrier.IsEmpty) return;

            carrier.ServerClear();
        }
    }
}
