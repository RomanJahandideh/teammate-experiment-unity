using Teammate.Networking;
using UnityEngine;

namespace Teammate.Gameplay
{
    /// <summary>
    /// Finds the nearest IInteractableStation in range and triggers it whenever
    /// PlayerMotor reports an Interact press. Runs only on the owning client — stations
    /// themselves are server-authoritative, this just decides *which* station a local
    /// button press targets.
    /// </summary>
    [RequireComponent(typeof(PlayerMotor))]
    public class PlayerStationInteractor : MonoBehaviour
    {
        [Tooltip("Radius within which a station can be interacted with.")]
        public float interactRadius = 1.6f;
        [Tooltip("Layer(s) world stations live on. Leave as Everything if you haven't set up layers.")]
        public LayerMask stationLayerMask = ~0;

        private PlayerMotor _motor;
        private readonly Collider[] _overlapBuffer = new Collider[8];

        void Awake()
        {
            _motor = GetComponent<PlayerMotor>();
        }

        void OnEnable() => _motor.InteractRequested += HandleInteractRequested;
        void OnDisable() => _motor.InteractRequested -= HandleInteractRequested;

        private void HandleInteractRequested(PlayerMotor source)
        {
            if (source != _motor) return; // Only react to our own motor's interact presses.

            var nearest = FindNearestStation();
            nearest?.ClientRequestInteract();
        }

        private IInteractableStation FindNearestStation()
        {
            int count = Physics.OverlapSphereNonAlloc(transform.position, interactRadius, _overlapBuffer, stationLayerMask);
            IInteractableStation best = null;
            float bestDistSqr = float.MaxValue;

            for (int i = 0; i < count; i++)
            {
                var station = _overlapBuffer[i].GetComponentInParent<IInteractableStation>();
                if (station == null) continue;

                float distSqr = (_overlapBuffer[i].transform.position - transform.position).sqrMagnitude;
                if (distSqr < bestDistSqr)
                {
                    bestDistSqr = distSqr;
                    best = station;
                }
            }

            return best;
        }
    }
}
