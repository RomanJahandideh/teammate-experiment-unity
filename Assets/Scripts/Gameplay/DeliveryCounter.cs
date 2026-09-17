using Unity.Netcode;
using UnityEngine;

namespace Teammate.Gameplay
{
    /// <summary>Deliver a plated dish here; resolved against the shared OrderQueue. The dish is consumed either way.</summary>
    [RequireComponent(typeof(NetworkObject))]
    public class DeliveryCounter : NetworkBehaviour, IInteractableStation
    {
        [Tooltip("The one OrderQueue for this mission's kitchen.")]
        public OrderQueue orderQueue;

        public string InteractPrompt => "Deliver";

        public void ClientRequestInteract() => RequestDeliverServerRpc();

        [ServerRpc(RequireOwnership = false)]
        private void RequestDeliverServerRpc(ServerRpcParams rpcParams = default)
        {
            var carrier = ServerPlayerLookup.GetCarrier(rpcParams.Receive.SenderClientId);
            if (carrier == null) return;

            var held = carrier.Carried.Value;
            if (held.Kind != ItemKind.PlatedDish) return;

            orderQueue.ServerTryDeliver(held.RecipeId);
            carrier.ServerClear();
            GameplayEvents.RaisePlayerAction(rpcParams.Receive.SenderClientId);
        }
    }
}
