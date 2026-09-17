using System;
using UnityEngine;

namespace Teammate.Physio
{
    /// <summary>
    /// Runs the "three-minute seated baseline calibration to set personal resting HR and
    /// HRV" described in the Study 2 methods, before classification can start.
    /// </summary>
    public class BaselineCalibrator : MonoBehaviour
    {
        [Header("Calibration window")]
        [Tooltip("Length of the seated calibration period, in seconds. Paper uses 3 minutes.")]
        public float calibrationDurationSeconds = 180f;

        [Header("Live status (read-only)")]
        public bool isCalibrating;
        public bool isComplete;
        public float elapsedSeconds;
        public double baselineHrBpm;
        public double baselineHrvRmssdMs;
        public int sampleCount;

        public event Action OnCalibrationStarted;
        public event Action<double, double> OnCalibrationComplete; // (baselineHr, baselineHrv)

        private double _hrSum;
        private double _hrvSum;

        public void BeginCalibration()
        {
            isCalibrating = true;
            isComplete = false;
            elapsedSeconds = 0f;
            sampleCount = 0;
            _hrSum = 0;
            _hrvSum = 0;
            OnCalibrationStarted?.Invoke();
        }

        /// <summary>Feed one HR/HRV sample while calibrating (call once per incoming watch sample).</summary>
        public void AddSample(double hrBpm, double hrvRmssdMs)
        {
            if (!isCalibrating || isComplete) return;

            _hrSum += hrBpm;
            _hrvSum += hrvRmssdMs;
            sampleCount++;
        }

        void Update()
        {
            if (!isCalibrating || isComplete) return;

            elapsedSeconds += Time.deltaTime;
            if (elapsedSeconds >= calibrationDurationSeconds)
            {
                CompleteCalibration();
            }
        }

        private void CompleteCalibration()
        {
            isCalibrating = false;
            isComplete = true;

            if (sampleCount > 0)
            {
                baselineHrBpm = _hrSum / sampleCount;
                baselineHrvRmssdMs = _hrvSum / sampleCount;
            }
            else
            {
                Debug.LogWarning("[BaselineCalibrator] Calibration window closed with zero samples; " +
                                  "baseline left at default (0). Check the watch bridge connection.");
            }

            Debug.Log($"[BaselineCalibrator] Baseline set: HR={baselineHrBpm:F1} bpm, HRV={baselineHrvRmssdMs:F1} ms RMSSD " +
                      $"from {sampleCount} samples.");
            OnCalibrationComplete?.Invoke(baselineHrBpm, baselineHrvRmssdMs);
        }

        /// <summary>Skip calibration and set a known baseline directly (e.g. loaded from a prior session, or a test harness).</summary>
        public void SetBaselineDirectly(double hrBpm, double hrvRmssdMs)
        {
            isCalibrating = false;
            isComplete = true;
            baselineHrBpm = hrBpm;
            baselineHrvRmssdMs = hrvRmssdMs;
            OnCalibrationComplete?.Invoke(baselineHrBpm, baselineHrvRmssdMs);
        }
    }
}
