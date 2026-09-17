using System.Collections.Generic;
using Teammate.Gameplay;
using Teammate.Interface;
using Teammate.Logging;
using Teammate.Networking;
using Unity.Netcode;
using UnityEngine;

namespace Teammate.Mission
{
    /// <summary>
    /// Server-authoritative mission lifecycle for one seven-minute block: starts/stops
    /// the order board and pressure-event scheduler, times the mission, and aggregates
    /// everything the study logs at mission end into one Mission_Behavior_Log.csv row.
    /// Drive it from an experimenter panel (BeginMission) between condition switches.
    /// </summary>
    [RequireComponent(typeof(NetworkObject))]
    public class MissionController : NetworkBehaviour
    {
        [Header("Wiring")]
        public OrderQueue orderQueue;
        public PressureEventScheduler pressureEventScheduler;
        public MissionBehaviorLogger behaviorLogger;
        public PressureEventLogger pressureEventLogger;

        [Header("Config")]
        public float missionDurationSeconds = 420f; // seven minutes
        public string teamId = "T00";
        public string conditionGroup = "A";

        public NetworkVariable<float> RemainingSeconds = new NetworkVariable<float>(
            0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        public NetworkVariable<bool> IsRunning = new NetworkVariable<bool>(
            false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        private int _missionNumber;
        private string _layoutCode;
        private string _layoutName;
        private double _missionStartServerTime;
        private System.DateTime _missionStartWallClock;
        private bool _abortedEarly;

        private int _ordersDelivered, _lateOrders, _wrongOrders, _burnedDishes;
        private double _scoreAccumulator;
        private readonly List<float> _supportResponseTimes = new List<float>();
        private readonly List<float> _recoveryTimes = new List<float>();
        private int _appropriateSupportCount;

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            if (!IsServer) return;

            if (orderQueue != null) orderQueue.OnDeliveryResolved += HandleDeliveryResolved;
            SubscribeCookPotBurns();
            if (pressureEventScheduler != null) pressureEventScheduler.OnEventResolved += HandlePressureEventResolved;
        }

        private void SubscribeCookPotBurns()
        {
            foreach (var pot in FindObjectsByType<CookPot>(FindObjectsSortMode.None))
                pot.OnCollectedBurned += (_, __) => _burnedDishes++;
        }

        /// <summary>Call from the experimenter panel to start a mission block.</summary>
        public void BeginMission(int missionNumber, DisplayCondition condition, string layoutCode, string layoutName)
        {
            if (!IsServer) return;

            _missionNumber = missionNumber;
            _layoutCode = layoutCode;
            _layoutName = layoutName;
            _missionStartServerTime = NetworkManager.ServerTime.Time;
            _missionStartWallClock = System.DateTime.Now;
            _abortedEarly = false;

            _ordersDelivered = _lateOrders = _wrongOrders = _burnedDishes = 0;
            _scoreAccumulator = 0;
            _supportResponseTimes.Clear();
            _recoveryTimes.Clear();
            _appropriateSupportCount = 0;

            foreach (var client in NetworkManager.ConnectedClientsList)
            {
                if (client.PlayerObject == null) continue;
                client.PlayerObject.GetComponent<IdleTracker>()?.ServerResetForNewMission();
                client.PlayerObject.GetComponent<CollisionTracker>()?.ServerResetForNewMission();
            }

            if (ConditionManager.Instance != null) ConditionManager.Instance.SetCondition(condition);
            orderQueue?.ServerClearAllOrders();
            orderQueue?.ServerBeginSpawning();
            pressureEventScheduler?.ServerArmForMission(_missionStartServerTime);

            RemainingSeconds.Value = missionDurationSeconds;
            IsRunning.Value = true;

            if (pressureEventLogger != null) pressureEventLogger.BeginMission(teamId, conditionGroup, missionNumber, condition, layoutCode, layoutName);
        }

        public void AbortMission()
        {
            if (!IsServer || !IsRunning.Value) return;
            _abortedEarly = true;
            EndMission();
        }

        void Update()
        {
            if (!IsServer || !IsRunning.Value) return;

            RemainingSeconds.Value = Mathf.Max(0f, RemainingSeconds.Value - Time.deltaTime);
            if (RemainingSeconds.Value <= 0f) EndMission();
        }

        private void EndMission()
        {
            IsRunning.Value = false;
            orderQueue?.ServerStopSpawning();
            pressureEventScheduler?.ServerDisarm();

            var idleValues = new List<float>();
            int collisionTotal = 0;
            int strategyChangesTotal = 0;

            foreach (var client in NetworkManager.ConnectedClientsList)
            {
                if (client.PlayerObject == null) continue;

                var idle = client.PlayerObject.GetComponent<IdleTracker>();
                if (idle != null) idleValues.Add(idle.GetCumulativeIdleSeconds());

                var collision = client.PlayerObject.GetComponent<CollisionTracker>();
                if (collision != null) collisionTotal += collision.BlockingCollisionCount.Value;

                var physio = client.PlayerObject.GetComponent<TeammatePhysioNetworkPlayer>();
                if (physio != null) strategyChangesTotal += physio.StrategyChangeCount.Value;
            }

            float avgIdle = idleValues.Count > 0 ? Average(idleValues) : 0f;
            float supportMean = _supportResponseTimes.Count > 0 ? Average(_supportResponseTimes) : 0f;
            float recoveryMean = _recoveryTimes.Count > 0 ? Average(_recoveryTimes) : 0f;

            var condition = ConditionManager.Instance != null ? ConditionManager.Instance.CurrentCondition.Value : DisplayCondition.NoCue;

            behaviorLogger?.LogMission(new MissionBehaviorRecord
            {
                TeamId = teamId,
                ConditionGroup = conditionGroup,
                MissionNumber = _missionNumber,
                Condition = condition,
                LayoutCode = _layoutCode,
                LayoutName = _layoutName,
                MissionStatus = _abortedEarly ? "Aborted" : "Completed",
                MissionStartTime = _missionStartWallClock,
                MissionEndTime = System.DateTime.Now,
                OrdersDelivered = _ordersDelivered,
                MissionScore = Mathf.RoundToInt((float)_scoreAccumulator),
                LateOrders = _lateOrders,
                BurnedDishes = _burnedDishes,
                WrongOrders = _wrongOrders,
                AvgPlayerIdleTimeSec = avgIdle,
                CollisionBlockingCount = collisionTotal,
                StrategyChangesCount = strategyChangesTotal,
                SupportResponseTimeMeanSec = supportMean,
                AppropriateSupportActionsCount = _appropriateSupportCount,
                FalseSupportActionsCount = 0, // not distinguishable from telemetry alone; left for researcher review
                RecoveryTimeMeanSec = recoveryMean,
            });
        }

        private static float Average(List<float> values)
        {
            float sum = 0f;
            foreach (var v in values) sum += v;
            return sum / values.Count;
        }

        private void HandleDeliveryResolved(DeliveryResult result, int orderId, int recipeId)
        {
            switch (result)
            {
                case DeliveryResult.OnTime:
                    _ordersDelivered++;
                    _scoreAccumulator += 100;
                    break;
                case DeliveryResult.Late:
                    _ordersDelivered++;
                    _lateOrders++;
                    _scoreAccumulator += 40;
                    break;
                case DeliveryResult.NoMatchingOrder:
                    _wrongOrders++;
                    break;
            }
        }

        private void HandlePressureEventResolved(PressureEventResult result)
        {
            if (result.HadSupport)
            {
                _supportResponseTimes.Add(result.SupportResponseTimeSeconds);
                if (result.Recovered) _appropriateSupportCount++;
            }
            if (result.Recovered)
                _recoveryTimes.Add(result.RecoveryTimeSeconds);

            pressureEventLogger?.LogEvent(result);
        }
    }
}
