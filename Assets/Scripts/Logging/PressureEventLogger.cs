using System.IO;
using System.Text;
using Teammate.Interface;
using Teammate.Mission;
using Teammate.Networking;
using UnityEngine;

namespace Teammate.Logging
{
    /// <summary>
    /// Appends one row per resolved pressure event, in exactly the column order already
    /// used by study_analysis/study2/Pressure_Event_Log.csv. Columns the original study
    /// coded from video/interview review (support_action_type detail beyond "Interaction",
    /// verbal_help_request_before_support, physio_cue_referenced) are left blank here for
    /// the researcher to fill in — this logger only writes what's objectively measurable
    /// from game telemetry.
    /// </summary>
    public class PressureEventLogger : MonoBehaviour
    {
        public string outputFileName = "Pressure_Event_Log_Live.csv";

        private static readonly string[] Header =
        {
            "team_id","condition_group","mission_number","condition_code","condition_name","layout_code","layout_name",
            "event_id","event_type","planned_event_time","target_role","trigger_description","actual_event_start_time",
            "first_support_time","support_response_time_sec","support_player_id","support_action_type",
            "appropriate_support_yes_no","recovery_time_sec","burned_or_failed_yes_no",
            "verbal_help_request_before_support_yes_no","physio_cue_referenced_yes_no","notes","researcher_initials"
        };

        private string _path;
        private string _teamId, _conditionGroup, _layoutCode, _layoutName;
        private int _missionNumber;
        private DisplayCondition _condition;
        private System.DateTime _missionWallClockStart;

        void Awake()
        {
            _path = Path.Combine(Application.persistentDataPath, outputFileName);
            if (!File.Exists(_path))
                File.WriteAllText(_path, string.Join(",", Header) + "\n", Encoding.UTF8);
        }

        public void BeginMission(string teamId, string conditionGroup, int missionNumber, DisplayCondition condition, string layoutCode, string layoutName)
        {
            _teamId = teamId;
            _conditionGroup = conditionGroup;
            _missionNumber = missionNumber;
            _condition = condition;
            _layoutCode = layoutCode;
            _layoutName = layoutName;
            _missionWallClockStart = System.DateTime.Now;
        }

        public void LogEvent(PressureEventResult result)
        {
            string supportPlayerId = result.HadSupport ? $"P{result.SupportClientId}" : "";
            string plannedTime = OffsetToClock(result.PlannedOffsetSeconds);
            string actualStart = OffsetToClock(result.ActualStartOffsetSeconds);
            string firstSupportTime = result.HadSupport ? OffsetToClock(result.ActualStartOffsetSeconds + result.SupportResponseTimeSeconds) : "";

            string row = string.Join(",", new[]
            {
                _teamId,
                _conditionGroup,
                _missionNumber.ToString(),
                ConditionCsvCodes.Code(_condition),
                Csv(ConditionCsvCodes.Name(_condition)),
                _layoutCode,
                Csv(_layoutName),
                $"E{result.EventId}",
                Csv(PressureEventCatalog.DisplayName(result.Type)),
                plannedTime,
                Csv(RoleLabel(result.TargetRole)),
                Csv(PressureEventCatalog.TriggerDescription(result.Type)),
                actualStart,
                firstSupportTime,
                result.HadSupport ? result.SupportResponseTimeSeconds.ToString("F1") : "",
                supportPlayerId,
                result.HadSupport ? "Interaction" : "", // coarse by design — see class summary
                result.HadSupport ? (result.Recovered ? "Yes" : "No") : "",
                result.Recovered ? result.RecoveryTimeSeconds.ToString("F1") : "",
                result.BurnedOrFailed ? "Yes" : "No",
                "", // verbal_help_request_before_support_yes_no — researcher, from audio/video
                "", // physio_cue_referenced_yes_no — researcher, from audio/video
                "",
                ""
            });

            File.AppendAllText(_path, row + "\n", Encoding.UTF8);
        }

        private string OffsetToClock(float offsetSeconds)
        {
            var t = _missionWallClockStart.AddSeconds(offsetSeconds);
            return t.ToString("HH:mm:ss");
        }

        private static string RoleLabel(PlayerRole r) => r switch
        {
            PlayerRole.Prep => "Prep Player",
            PlayerRole.Cook => "Cook Player",
            PlayerRole.Runner => "Runner Player",
            _ => "Unassigned",
        };

        private static string Csv(string value)
        {
            if (string.IsNullOrEmpty(value)) return "";
            return value.Contains(",") || value.Contains("\"")
                ? "\"" + value.Replace("\"", "\"\"") + "\""
                : value;
        }
    }
}
