using System;
using Teammate.Interface;

namespace Teammate.Logging
{
    /// <summary>Everything MissionController gathers for one Mission_Behavior_Log.csv row.</summary>
    public sealed class MissionBehaviorRecord
    {
        public string TeamId;
        public string ConditionGroup;
        public int MissionNumber;
        public DisplayCondition Condition;
        public string LayoutCode;
        public string LayoutName;
        public string MissionStatus;
        public DateTime MissionStartTime;
        public DateTime MissionEndTime;

        public int OrdersDelivered;
        public int MissionScore;
        public int LateOrders;
        public int BurnedDishes;
        public int WrongOrders;
        public float AvgPlayerIdleTimeSec;
        public int CollisionBlockingCount;
        public int StrategyChangesCount;
        public float SupportResponseTimeMeanSec;
        public int AppropriateSupportActionsCount;
        public int FalseSupportActionsCount;
        public float RecoveryTimeMeanSec;
    }

    public static class ConditionCsvCodes
    {
        public static string Code(DisplayCondition c) => c switch
        {
            DisplayCondition.NoCue => "C1",
            DisplayCondition.NumericCue => "C2",
            DisplayCondition.IconCue => "C3",
            _ => "C?",
        };

        // Matches the wording already used in this study's existing CSV corpus.
        public static string Name(DisplayCondition c) => c switch
        {
            DisplayCondition.NoCue => "No Physiological Cue",
            DisplayCondition.NumericCue => "Numeric Physiological Cue",
            DisplayCondition.IconCue => "Animated Avatar Cue",
            _ => c.ToString(),
        };
    }
}
