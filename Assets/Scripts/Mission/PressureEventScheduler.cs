using System;
using System.Collections.Generic;
using Teammate.Gameplay;
using Teammate.Networking;
using Unity.Netcode;
using UnityEngine;

namespace Teammate.Mission
{
    /// <summary>
    /// Fires the three scripted pressure events at their planned offsets into a mission
    /// (Section "Mission Layouts, Scripted Pressure Events, and Procedure") and measures
    /// support response time / recovery time / burned-or-failed for each. Server-only.
    /// </summary>
    [RequireComponent(typeof(NetworkObject))]
    public class PressureEventScheduler : NetworkBehaviour
    {
        [Header("Wiring")]
        public OrderQueue orderQueue;
        [Tooltip("Two pots that become simultaneously active for Pot Overload.")]
        public CookPot potA;
        public CookPot potB;
        [Tooltip("Burn window applied to the two forced pots — tighter than their normal setting, since this is meant to be urgent.")]
        public float potOverloadBurnAfterSeconds = 8f;
        public int serviceRushOrderCount = 3;
        [Tooltip("Safety cap: an event resolves as unrecovered/failed if nothing clears it within this many seconds.")]
        public float maxEventObservationSeconds = 90f;

        public event Action<PressureEventResult> OnEventResolved;

        private double _missionStartServerTime;
        private bool _armed;
        private readonly bool[] _fired = new bool[3];
        private RuntimeState _active;
        private int _nextEventId = 1;

        private sealed class RuntimeState
        {
            public int EventId;
            public PressureEventType Type;
            public double StartServerTime;
            public bool HasSupport;
            public double SupportServerTime;
            public ulong SupportClientId;

            // Ingredient Bottleneck / Service Rush
            public HashSet<int> PendingOrderIds;
            public bool AnyPendingOrderExpired;

            // Pot Overload
            public bool WatchingPots;
            public bool AnyPotBurned;
        }

        void OnEnable()
        {
            GameplayEvents.OnPlayerAction += HandlePlayerAction;
        }

        void OnDisable()
        {
            GameplayEvents.OnPlayerAction -= HandlePlayerAction;
            UnhookOrderQueue();
            UnhookPots();
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            if (!IsServer) return;

            if (orderQueue != null)
            {
                orderQueue.OnDeliveryResolved += HandleOrderDeliveryResolved;
                orderQueue.OnOrderExpired += HandleOrderExpired;
            }
            if (potA != null) potA.OnCollectedBurned += HandlePotBurned;
            if (potB != null) potB.OnCollectedBurned += HandlePotBurned;
        }

        private void UnhookOrderQueue()
        {
            if (orderQueue == null) return;
            orderQueue.OnDeliveryResolved -= HandleOrderDeliveryResolved;
            orderQueue.OnOrderExpired -= HandleOrderExpired;
        }

        private void UnhookPots()
        {
            if (potA != null) potA.OnCollectedBurned -= HandlePotBurned;
            if (potB != null) potB.OnCollectedBurned -= HandlePotBurned;
        }

        public void ServerArmForMission(double missionStartServerTime)
        {
            if (!IsServer) return;
            _missionStartServerTime = missionStartServerTime;
            _armed = true;
            for (int i = 0; i < _fired.Length; i++) _fired[i] = false;
            _active = null;
        }

        public void ServerDisarm() => _armed = false;

        void Update()
        {
            if (!IsServer || !_armed) return;

            double elapsed = NetworkManager.ServerTime.Time - _missionStartServerTime;

            TryFire(PressureEventType.IngredientBottleneck, elapsed, 0);
            TryFire(PressureEventType.PotOverload, elapsed, 1);
            TryFire(PressureEventType.ServiceRush, elapsed, 2);

            CheckActiveResolution();
        }

        private void TryFire(PressureEventType type, double elapsedSeconds, int index)
        {
            if (_fired[index] || elapsedSeconds < PressureEventCatalog.PlannedOffsetSeconds(type)) return;

            _fired[index] = true;
            FireEvent(type);
        }

        private void FireEvent(PressureEventType type)
        {
            if (_active != null) ResolveActive(); // shouldn't normally overlap given the paper's offsets, but don't lose one if it does

            var state = new RuntimeState
            {
                EventId = _nextEventId++,
                Type = type,
                StartServerTime = NetworkManager.ServerTime.Time,
            };

            switch (type)
            {
                case PressureEventType.IngredientBottleneck:
                    var pair = orderQueue.ServerForceSpawnDuplicatePair();
                    state.PendingOrderIds = new HashSet<int> { pair.orderIdA, pair.orderIdB };
                    break;

                case PressureEventType.PotOverload:
                    bool a = potA != null && potA.ServerForceOverload(IngredientType.Greens, potOverloadBurnAfterSeconds);
                    bool b = potB != null && potB.ServerForceOverload(IngredientType.Greens, potOverloadBurnAfterSeconds);
                    if (!a && !b) return; // both pots already busy with real player work; skip rather than fabricate an unobservable event
                    state.WatchingPots = true;
                    break;

                case PressureEventType.ServiceRush:
                    var ids = orderQueue.ServerForceSpawnRush(serviceRushOrderCount);
                    state.PendingOrderIds = new HashSet<int>(ids);
                    break;
            }

            _active = state;
        }

        private void HandlePlayerAction(ulong clientId)
        {
            if (!IsServer || _active == null || _active.HasSupport) return;

            var physioPlayer = ServerPlayerLookup.GetPhysioPlayer(clientId);
            if (physioPlayer == null) return;
            if (physioPlayer.Role.Value == PressureEventCatalog.TargetRole(_active.Type)) return; // the overloaded role's own actions aren't "support"

            _active.HasSupport = true;
            _active.SupportServerTime = NetworkManager.ServerTime.Time;
            _active.SupportClientId = clientId;
        }

        private void HandleOrderDeliveryResolved(DeliveryResult result, int orderId, int recipeId)
        {
            if (_active?.PendingOrderIds == null) return;
            _active.PendingOrderIds.Remove(orderId);
        }

        private void HandleOrderExpired(OrderTicket order)
        {
            if (_active?.PendingOrderIds == null) return;
            if (_active.PendingOrderIds.Remove(order.OrderId))
                _active.AnyPendingOrderExpired = true;
        }

        private void HandlePotBurned(ulong clientId, IngredientType ingredient)
        {
            if (_active is { WatchingPots: true })
                _active.AnyPotBurned = true;
        }

        private void CheckActiveResolution()
        {
            if (_active == null) return;

            double elapsedSinceStart = NetworkManager.ServerTime.Time - _active.StartServerTime;
            bool timedOut = elapsedSinceStart > maxEventObservationSeconds;
            bool naturallyResolved =
                (_active.WatchingPots && (potA == null || !potA.IsOccupied) && (potB == null || !potB.IsOccupied)) ||
                (_active.PendingOrderIds != null && _active.PendingOrderIds.Count == 0);

            if (naturallyResolved || timedOut)
                ResolveActive(forcedFailureFromTimeout: timedOut && !naturallyResolved);
        }

        private void ResolveActive(bool forcedFailureFromTimeout = false)
        {
            var state = _active;
            _active = null;
            if (state == null) return;

            bool burnedOrFailed = state.AnyPotBurned || state.AnyPendingOrderExpired || forcedFailureFromTimeout;

            var result = new PressureEventResult
            {
                EventId = state.EventId,
                Type = state.Type,
                TargetRole = PressureEventCatalog.TargetRole(state.Type),
                PlannedOffsetSeconds = PressureEventCatalog.PlannedOffsetSeconds(state.Type),
                ActualStartOffsetSeconds = (float)(state.StartServerTime - _missionStartServerTime),
                HadSupport = state.HasSupport,
                SupportResponseTimeSeconds = state.HasSupport ? (float)(state.SupportServerTime - state.StartServerTime) : 0f,
                SupportClientId = state.SupportClientId,
                Recovered = !forcedFailureFromTimeout,
                RecoveryTimeSeconds = (float)(NetworkManager.ServerTime.Time - state.StartServerTime),
                BurnedOrFailed = burnedOrFailed,
            };

            OnEventResolved?.Invoke(result);
        }
    }
}
