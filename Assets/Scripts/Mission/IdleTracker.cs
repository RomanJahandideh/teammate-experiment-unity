using Teammate.Gameplay;
using Unity.Netcode;
using UnityEngine;

namespace Teammate.Mission
{
    /// <summary>
    /// Server-side only: accumulates how long this player has gone without moving or
    /// acting, for the mission's avg_player_idle_time_sec. Reads the server's own copy of
    /// the player's transform (kept current by NetworkTransform regardless of movement
    /// authority mode), so this works whether movement ends up owner- or
    /// server-authoritative.
    /// </summary>
    [RequireComponent(typeof(NetworkObject))]
    public class IdleTracker : NetworkBehaviour
    {
        [Tooltip("Minimum movement per second to count as 'not idle'.")]
        public float movementEpsilon = 0.05f;

        private Vector3 _lastPosition;
        private float _cumulativeIdleSeconds;
        private float _secondsSinceLastActivity;

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            if (!IsServer) { enabled = false; return; }

            _lastPosition = transform.position;
            GameplayEvents.OnPlayerAction += HandlePlayerAction;
        }

        public override void OnNetworkDespawn()
        {
            GameplayEvents.OnPlayerAction -= HandlePlayerAction;
            base.OnNetworkDespawn();
        }

        private void HandlePlayerAction(ulong clientId)
        {
            if (clientId == OwnerClientId)
                _secondsSinceLastActivity = 0f; // an interaction counts as activity even while standing still
        }

        void Update()
        {
            if (!IsServer) return;

            float moved = Vector3.Distance(transform.position, _lastPosition);
            _lastPosition = transform.position;

            bool moving = moved / Mathf.Max(Time.deltaTime, 0.0001f) >= movementEpsilon;
            if (moving)
                _secondsSinceLastActivity = 0f;
            else
                _secondsSinceLastActivity += Time.deltaTime;

            // Idle = no movement AND no recent interaction for this whole frame's worth of time.
            if (!moving && _secondsSinceLastActivity > 0f)
                _cumulativeIdleSeconds += Time.deltaTime;
        }

        /// <summary>Total seconds this player has spent idle (no movement, no interaction) since the last reset (server-only).</summary>
        public float GetCumulativeIdleSeconds() => _cumulativeIdleSeconds;

        public void ServerResetForNewMission()
        {
            _cumulativeIdleSeconds = 0f;
            _secondsSinceLastActivity = 0f;
            _lastPosition = transform.position;
        }
    }
}
