using System;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Teammate.Interface
{
    /// <summary>
    /// Experimenter-controlled switch between No Cue / Numeric Cue / Icon Cue for the
    /// current mission block. Server-authoritative so every client shows the same
    /// condition at the same time; the experimenter's machine should run as (or alongside)
    /// the host. Bind condition changes to your Latin-square running order sheet.
    ///
    /// Number keys 1/2/3 are a convenience for running sessions without a custom UI:
    /// 1 = No Cue, 2 = Numeric Cue, 3 = Icon Cue.
    /// </summary>
    public class ConditionManager : NetworkBehaviour
    {
        public static ConditionManager Instance { get; private set; }

        public NetworkVariable<DisplayCondition> CurrentCondition = new NetworkVariable<DisplayCondition>(
            DisplayCondition.NoCue, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        public event Action<DisplayCondition> OnConditionChanged;

        void Awake()
        {
            Instance = this;
        }

        public override void OnNetworkSpawn()
        {
            CurrentCondition.OnValueChanged += (_, next) => OnConditionChanged?.Invoke(next);
        }

        void Update()
        {
            if (!IsServer) return;

            var keyboard = Keyboard.current;
            if (keyboard == null) return;

            if (keyboard.digit1Key.wasPressedThisFrame) SetCondition(DisplayCondition.NoCue);
            else if (keyboard.digit2Key.wasPressedThisFrame) SetCondition(DisplayCondition.NumericCue);
            else if (keyboard.digit3Key.wasPressedThisFrame) SetCondition(DisplayCondition.IconCue);
        }

        /// <summary>Call from a mission-scheduling script, or the number-key shortcuts above.</summary>
        public void SetCondition(DisplayCondition condition)
        {
            if (!IsServer)
            {
                Debug.LogWarning("[ConditionManager] Only the host/server may change the study condition.");
                return;
            }

            CurrentCondition.Value = condition;
            Debug.Log($"[ConditionManager] Condition set to {condition}");
        }
    }
}
