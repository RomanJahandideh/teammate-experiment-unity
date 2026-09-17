using Unity.Netcode;
using UnityEngine;

namespace Teammate.Gameplay
{
    /// <summary>Cooked ingredient in, plated dish out — matched against whichever DishRecipe uses that ingredient.</summary>
    [RequireComponent(typeof(NetworkObject))]
    public class PlatingCounter : NetworkBehaviour, IInteractableStation
    {
        public DishRecipe[] availableRecipes;

        public string InteractPrompt => "Plate";

        public void ClientRequestInteract() => RequestPlateServerRpc();

        [ServerRpc(RequireOwnership = false)]
        private void RequestPlateServerRpc(ServerRpcParams rpcParams = default)
        {
            var carrier = ServerPlayerLookup.GetCarrier(rpcParams.Receive.SenderClientId);
            if (carrier == null) return;

            var held = carrier.Carried.Value;
            if (held.Kind != ItemKind.CookedIngredient) return;

            var recipe = FindRecipeFor(held.Ingredient);
            if (recipe == null) return; // no dish on the schedule wants this ingredient; use the trash can instead

            carrier.ServerSetCarried(new CarriedItem
            {
                Kind = ItemKind.PlatedDish,
                Ingredient = held.Ingredient,
                RecipeId = recipe.recipeId,
            });
            GameplayEvents.RaisePlayerAction(rpcParams.Receive.SenderClientId);
        }

        private DishRecipe FindRecipeFor(IngredientType ingredient)
        {
            if (availableRecipes == null) return null;
            foreach (var recipe in availableRecipes)
                if (recipe != null && recipe.requiredIngredient == ingredient)
                    return recipe;
            return null;
        }
    }
}
