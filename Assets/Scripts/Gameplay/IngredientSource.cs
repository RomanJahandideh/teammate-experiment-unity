using Unity.Netcode;
using UnityEngine;

namespace Teammate.Gameplay
{
    /// <summary>Unlimited-supply ingredient crate: interact while empty-handed to pick up one raw ingredient.</summary>
    [RequireComponent(typeof(NetworkObject))]
    public class IngredientSource : NetworkBehaviour, IInteractableStation
    {
        public IngredientType suppliedType = IngredientType.Greens;

        public string InteractPrompt => $"Pick up {suppliedType}";

        public void ClientRequestInteract() => RequestPickupServerRpc();

        [ServerRpc(RequireOwnership = false)]
        private void RequestPickupServerRpc(ServerRpcParams rpcParams = default)
        {
            var carrier = ServerPlayerLookup.GetCarrier(rpcParams.Receive.SenderClientId);
            if (carrier == null || !carrier.IsEmpty) return;

            carrier.ServerSetCarried(new CarriedItem { Kind = ItemKind.RawIngredient, Ingredient = suppliedType, RecipeId = -1 });
            GameplayEvents.RaisePlayerAction(rpcParams.Receive.SenderClientId);
        }
    }
}
