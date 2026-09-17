using System;
using System.IO;
using System.Text;
using Teammate.Interface;
using Teammate.Networking;
using Teammate.Physio;
using UnityEngine;

namespace Teammate.Logging
{
    /// <summary>
    /// Writes one CSV row per 1 Hz physiological sample for the local player, in exactly
    /// the column layout already used by the study's analysis pipeline
    /// (study_analysis/study2/Physio_System_Log.csv), so a real session run through this
    /// code drops straight into the existing analysis scripts with no reshaping.
    ///
    /// Runs on each participant's own client and logs only that participant's own data —
    /// consistent with the "all processing kept on the client device" ethics note.
    /// </summary>
    [RequireComponent(typeof(LocalPhysioController))]
    public class PhysioSessionLogger : MonoBehaviour
    {
        [Header("Session metadata (set by the experimenter UI / config before a mission starts)")]
        public string teamId = "T00";
        public string participantId = "P000";
        public PlayerRole role = PlayerRole.Unassigned;
        public int missionNumber = 1;
        [Tooltip("C1 = No Cue, C2 = Numeric Cue, C3 = Icon Cue, matching the study's condition_code column.")]
        public string conditionCode = "C1";
        public string layoutCode = "L1";

        [Header("Output")]
        [Tooltip("Relative to Application.persistentDataPath.")]
        public string outputFileName = "Physio_System_Log_Live.csv";

        private static readonly string[] Header =
        {
            "timestamp","team_id","participant_id","role","mission_number","condition_code","layout_code",
            "raw_hr_bpm","raw_hrv_or_rmssd_ms","hr_baseline_bpm","hrv_baseline_rmssd_ms",
            "hr_state","hrv_state","displayed_hr_state","displayed_hrv_state","icon_state_id",
            "numeric_value_visible_yes_no","sensor_latency_ms","missing_data_flag","dropout_duration_sec",
            "state_change_yes_no","notes"
        };

        private LocalPhysioController _physio;
        private AppleWatchPhysioReceiver _receiver;
        private string _path;
        private DateTime _missionStart;
        private HRState _lastLoggedHr;
        private HRVState _lastLoggedHrv;
        private bool _hasLoggedOnce;
        private float _dropoutStartedAt = -1f;

        void Awake()
        {
            _physio = GetComponent<LocalPhysioController>();
            _receiver = GetComponent<AppleWatchPhysioReceiver>();
            _path = Path.Combine(Application.persistentDataPath, outputFileName);
            EnsureHeader();
        }

        void OnEnable()
        {
            _physio.OnRawSample += HandleRawSample;
            if (_receiver != null)
                _receiver.OnDropoutDetected += HandleDropoutDetected;
        }

        void OnDisable()
        {
            _physio.OnRawSample -= HandleRawSample;
            if (_receiver != null)
                _receiver.OnDropoutDetected -= HandleDropoutDetected;
        }

        private void HandleDropoutDetected() => _dropoutStartedAt = Time.time;

        /// <summary>Call at the start of each seven-minute mission block.</summary>
        public void BeginMission(int missionNo, string condCode, string layoutCodeValue)
        {
            missionNumber = missionNo;
            conditionCode = condCode;
            layoutCode = layoutCodeValue;
            _missionStart = DateTime.UtcNow;
            _hasLoggedOnce = false;
        }

        private void EnsureHeader()
        {
            if (!File.Exists(_path))
                File.WriteAllText(_path, string.Join(",", Header) + "\n", Encoding.UTF8);
        }

        private void HandleRawSample(double hrBpm, double hrvMs, HRState hrState, HRVState hrvState, double latencyMs)
        {
            bool stateChanged = !_hasLoggedOnce || hrState != _lastLoggedHr || hrvState != _lastLoggedHrv;
            _lastLoggedHr = hrState;
            _lastLoggedHrv = hrvState;
            _hasLoggedOnce = true;

            var displayedHr = _physio.displayedHr;
            var displayedHrv = _physio.displayedHrv;

            var condition = ConditionManager.Instance != null
                ? ConditionManager.Instance.CurrentCondition.Value
                : DisplayCondition.NoCue;

            bool iconVisible = condition == DisplayCondition.IconCue;
            bool numericVisible = condition == DisplayCondition.NumericCue;

            float dropoutDuration = 0f;
            bool missingData = false;
            if (_dropoutStartedAt >= 0f)
            {
                dropoutDuration = Time.time - _dropoutStartedAt;
                missingData = true;
                _dropoutStartedAt = -1f; // one-shot; the next sample after a gap logs the gap length once
            }

            var classification = new PhysioClassification(displayedHr, displayedHrv);

            string timestamp = (DateTime.UtcNow - _missionStart).ToString(@"hh\:mm\:ss");

            string row = string.Join(",", new[]
            {
                timestamp,
                teamId,
                participantId,
                RoleLabel(role),
                missionNumber.ToString(),
                conditionCode,
                layoutCode,
                hrBpm.ToString("F1"),
                hrvMs.ToString("F1"),
                _physio.baselineCalibrator != null ? _physio.baselineCalibrator.baselineHrBpm.ToString("F0") : "",
                _physio.baselineCalibrator != null ? _physio.baselineCalibrator.baselineHrvRmssdMs.ToString("F1") : "",
                PhysioStateCodes.Code(hrState),
                PhysioStateCodes.Code(hrvState),
                iconVisible ? PhysioStateCodes.Code(displayedHr) : "",
                iconVisible ? PhysioStateCodes.Code(displayedHrv) : "",
                iconVisible ? classification.IconStateId.ToString() : "",
                numericVisible ? "Yes" : "No",
                latencyMs >= 0 ? latencyMs.ToString("F0") : "",
                missingData ? "Yes" : "No",
                dropoutDuration.ToString("F1"),
                stateChanged ? "Yes" : "No",
                ""
            });

            File.AppendAllText(_path, row + "\n", Encoding.UTF8);
        }

        private static string RoleLabel(PlayerRole r) => r switch
        {
            PlayerRole.Prep => "Prep Player",
            PlayerRole.Cook => "Cook Player",
            PlayerRole.Runner => "Runner Player",
            _ => "Unassigned",
        };
    }
}
