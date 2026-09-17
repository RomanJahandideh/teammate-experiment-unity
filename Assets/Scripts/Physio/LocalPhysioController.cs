using System;
using UnityEngine;

namespace Teammate.Physio
{
    /// <summary>
    /// Runs entirely on the local player's own client: reads the Apple Watch bridge,
    /// tracks that player's own baseline, classifies each 1 Hz sample against it, and
    /// debounces the result into the displayed HR/HRV state. This is the only object
    /// from which the networking layer is allowed to read a player's physiological
    /// state (see TeammatePhysioNetworkPlayer) — never the raw bpm/RMSSD values.
    /// </summary>
    [RequireComponent(typeof(AppleWatchPhysioReceiver))]
    public class LocalPhysioController : MonoBehaviour
    {
        [Header("Wiring")]
        public BaselineCalibrator baselineCalibrator;

        [Header("Debounce")]
        [Tooltip("Consecutive 1 Hz samples a new state must hold before the displayed cue changes.")]
        public int debounceSamples = 3;

        [Header("Live status (read-only)")]
        public HRState classifiedHr;
        public HRVState classifiedHrv;
        public HRState displayedHr;
        public HRVState displayedHrv;

        /// <summary>Fires whenever the debounced, displayed state changes. This is what the network layer subscribes to.</summary>
        public event Action<HRState, HRVState> OnDisplayedStateChanged;

        /// <summary>Fires on every raw sample (post-classification, pre-debounce) — useful for the session logger.</summary>
        public event Action<double, double, HRState, HRVState, double> OnRawSample; // (hrBpm, hrvMs, hrState, hrvState, latencyMs)

        private AppleWatchPhysioReceiver _receiver;
        private StateDebouncer<HRState> _hrDebouncer;
        private StateDebouncer<HRVState> _hrvDebouncer;

        void Awake()
        {
            _receiver = GetComponent<AppleWatchPhysioReceiver>();
            _hrDebouncer = new StateDebouncer<HRState>(debounceSamples);
            _hrvDebouncer = new StateDebouncer<HRVState>(debounceSamples);
        }

        void OnEnable()
        {
            _receiver.OnSample += HandleSample;
            if (baselineCalibrator != null)
                baselineCalibrator.OnCalibrationComplete += HandleBaselineReady;
        }

        void OnDisable()
        {
            if (_receiver != null) _receiver.OnSample -= HandleSample;
            if (baselineCalibrator != null)
                baselineCalibrator.OnCalibrationComplete -= HandleBaselineReady;
        }

        private void HandleBaselineReady(double baselineHr, double baselineHrv) =>
            Debug.Log($"[LocalPhysioController] Baseline ready (HR={baselineHr:F1} bpm, HRV={baselineHrv:F1} ms); classification active.");

        private void HandleSample(double hrBpm, double hrvMs, double latencyMs)
        {
            if (baselineCalibrator == null)
            {
                Debug.LogWarning("[LocalPhysioController] No BaselineCalibrator assigned; cannot classify.");
                return;
            }

            // Feed calibration while it's running; skip classification until a baseline exists.
            if (baselineCalibrator.isCalibrating)
            {
                baselineCalibrator.AddSample(hrBpm, hrvMs);
                return;
            }

            if (!baselineCalibrator.isComplete) return;

            classifiedHr = PhysioClassifier.ClassifyHr(hrBpm, baselineCalibrator.baselineHrBpm);
            classifiedHrv = PhysioClassifier.ClassifyHrv(hrvMs, baselineCalibrator.baselineHrvRmssdMs);

            OnRawSample?.Invoke(hrBpm, hrvMs, classifiedHr, classifiedHrv, latencyMs);

            bool hrChanged = _hrDebouncer.Sample(classifiedHr);
            bool hrvChanged = _hrvDebouncer.Sample(classifiedHrv);

            displayedHr = _hrDebouncer.Displayed;
            displayedHrv = _hrvDebouncer.Displayed;

            if (hrChanged || hrvChanged)
                OnDisplayedStateChanged?.Invoke(displayedHr, displayedHrv);
        }
    }
}
