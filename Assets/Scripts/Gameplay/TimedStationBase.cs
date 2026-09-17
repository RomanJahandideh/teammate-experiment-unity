using System;
using Unity.Netcode;
using UnityEngine;

namespace Teammate.Gameplay
{
    /// <summary>Server-replicated state for one processing slot (chopping board or pot).</summary>
    public struct StationSlotState
    {
        public bool Occupied;
        public IngredientType Ingredient;
        public double ProcessStartServerTime;
    }

    /// <summary>
    /// Shared "place an item, wait, collect the result" logic behind both ChoppingStation
    /// (Raw → Prepped, never burns) and CookPot (Prepped → Cooked, burns if left too
    /// long — this is the station the Pot Overload pressure event targets). One
    /// interaction places an item from an empty-handed player; the next interaction, once
    /// ready, collects the result into an empty-handed player's hands.
    /// </summary>
    public abstract class TimedStationBase : NetworkBehaviour, IInteractableStation
    {
        [Tooltip("Seconds after placement until the result is ready to collect.")]
        public float readyAfterSeconds = 3f;
        [Tooltip("Seconds after placement until the result burns if not collected. <= 0 means it never burns.")]
        public float burnAfterSeconds = -1f;

        public NetworkVariable<StationSlotState> Slot = new NetworkVariable<StationSlotState>(
            default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        /// <summary>Server-side only: fired when a result is collected burned. (targetClientId, ingredient)</summary>
        public event Action<ulong, IngredientType> OnCollectedBurned;

        protected abstract ItemKind InputKind { get; }
        protected abstract ItemKind ReadyOutputKind { get; }
        protected abstract ItemKind BurnedOutputKind { get; }

        public virtual string InteractPrompt => Slot.Value.Occupied ? "Collect" : "Place";
        public bool IsOccupied => Slot.Value.Occupied;

        private float? _forcedBurnOverrideSeconds;

        public void ClientRequestInteract() => RequestInteractServerRpc();

        /// <summary>
        /// Server-only: activates this station without a player placing anything, with an
        /// optional tighter burn window — how PressureEventScheduler implements Pot
        /// Overload (two pots become urgent at once). No-ops if already occupied.
        /// </summary>
        public bool ServerForceOverload(IngredientType ingredient, float? overrideBurnAfterSeconds = null)
        {
            if (!IsServer || Slot.Value.Occupied) return false;

            Slot.Value = new StationSlotState
            {
                Occupied = true,
                Ingredient = ingredient,
                ProcessStartServerTime = NetworkManager.ServerTime.Time,
            };
            _forcedBurnOverrideSeconds = overrideBurnAfterSeconds;
            return true;
        }

        [ServerRpc(RequireOwnership = false)]
        private void RequestInteractServerRpc(ServerRpcParams rpcParams = default)
        {
            ulong senderId = rpcParams.Receive.SenderClientId;
            var carrier = ServerPlayerLookup.GetCarrier(senderId);
            if (carrier == null) return;

            if (!Slot.Value.Occupied)
            {
                TryPlace(carrier, senderId);
            }
            else
            {
                TryCollect(carrier, senderId);
            }
        }

        private void TryPlace(PlayerCarrier carrier, ulong senderId)
        {
            var held = carrier.Carried.Value;
            if (held.Kind != InputKind) return;

            Slot.Value = new StationSlotState
            {
                Occupied = true,
                Ingredient = held.Ingredient,
                ProcessStartServerTime = NetworkManager.ServerTime.Time,
            };
            carrier.ServerClear();
            GameplayEvents.RaisePlayerAction(senderId);
        }

        private void TryCollect(PlayerCarrier carrier, ulong senderId)
        {
            if (!carrier.IsEmpty) return;

            double elapsed = NetworkManager.ServerTime.Time - Slot.Value.ProcessStartServerTime;
            if (elapsed < readyAfterSeconds) return; // not ready yet

            float effectiveBurnAfter = _forcedBurnOverrideSeconds ?? burnAfterSeconds;
            bool burned = effectiveBurnAfter > 0f && elapsed > effectiveBurnAfter;
            var resultKind = burned ? BurnedOutputKind : ReadyOutputKind;
            var ingredient = Slot.Value.Ingredient;

            carrier.ServerSetCarried(new CarriedItem { Kind = resultKind, Ingredient = ingredient, RecipeId = -1 });
            Slot.Value = default;
            _forcedBurnOverrideSeconds = null;
            GameplayEvents.RaisePlayerAction(senderId);

            if (burned)
                OnCollectedBurned?.Invoke(senderId, ingredient);
        }

        /// <summary>0-1 progress toward ready, for a fill-bar visual. Safe to call on any client.</summary>
        public float NormalizedProgress()
        {
            var slot = Slot.Value;
            if (!slot.Occupied || readyAfterSeconds <= 0f) return 0f;

            double elapsed = NetworkManager.ServerTime.Time - slot.ProcessStartServerTime;
            return Mathf.Clamp01((float)(elapsed / readyAfterSeconds));
        }
    }
}
