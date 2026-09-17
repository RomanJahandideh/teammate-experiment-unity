using System.IO;
using System.Text;
using UnityEngine;

namespace Teammate.Logging
{
    /// <summary>
    /// Appends one row per finished mission block, in exactly the column order already
    /// used by study_analysis/study2/Mission_Behavior_Log.csv. Run this on the host
    /// machine only (MissionController is server-only logic).
    /// </summary>
    public class MissionBehaviorLogger : MonoBehaviour
    {
        public string outputFileName = "Mission_Behavior_Log_Live.csv";

        private static readonly string[] Header =
        {
            "team_id","condition_group","mission_number","condition_code","condition_name","layout_code","layout_name",
            "mission_status","mission_start_time","mission_end_time","orders_delivered","mission_score","late_orders",
            "burned_dishes","wrong_orders","avg_player_idle_time_sec","collision_blocking_count",
            "task_contribution_balance_notes","strategy_changes_count","support_response_time_mean_sec",
            "appropriate_support_actions_count","false_support_actions_count","recovery_time_mean_sec",
            "technical_issues","researcher_notes"
        };

        private string _path;

        void Awake()
        {
            _path = Path.Combine(Application.persistentDataPath, outputFileName);
            if (!File.Exists(_path))
                File.WriteAllText(_path, string.Join(",", Header) + "\n", Encoding.UTF8);
        }

        public void LogMission(MissionBehaviorRecord r)
        {
            string row = string.Join(",", new[]
            {
                r.TeamId,
                r.ConditionGroup,
                r.MissionNumber.ToString(),
                ConditionCsvCodes.Code(r.Condition),
                Csv(ConditionCsvCodes.Name(r.Condition)),
                r.LayoutCode,
                Csv(r.LayoutName),
                r.MissionStatus,
                r.MissionStartTime.ToString("HH:mm:ss"),
                r.MissionEndTime.ToString("HH:mm:ss"),
                r.OrdersDelivered.ToString(),
                r.MissionScore.ToString(),
                r.LateOrders.ToString(),
                r.BurnedDishes.ToString(),
                r.WrongOrders.ToString(),
                r.AvgPlayerIdleTimeSec.ToString("F1"),
                r.CollisionBlockingCount.ToString(),
                "", // task_contribution_balance_notes — researcher-authored from observation
                r.StrategyChangesCount.ToString(),
                r.SupportResponseTimeMeanSec.ToString("F1"),
                r.AppropriateSupportActionsCount.ToString(),
                r.FalseSupportActionsCount.ToString(),
                r.RecoveryTimeMeanSec.ToString("F1"),
                "None",
                ""
            });

            File.AppendAllText(_path, row + "\n", Encoding.UTF8);
        }

        private static string Csv(string value)
        {
            if (string.IsNullOrEmpty(value)) return "";
            return value.Contains(",") || value.Contains("\"")
                ? "\"" + value.Replace("\"", "\"\"") + "\""
                : value;
        }
    }
}
