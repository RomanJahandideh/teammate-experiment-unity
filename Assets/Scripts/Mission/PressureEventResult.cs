using Teammate.Networking;

namespace Teammate.Mission
{
    /// <summary>
    /// Everything PressureEventLogger needs for one Pressure_Event_Log.csv row.
    /// Fields we can measure objectively (timing, who responded, burned/failed) are
    /// filled in automatically; fields the original study coded from video/interview
    /// review (support_action_type detail, verbal_help_request, physio_cue_referenced)
    /// are left for the researcher to fill in by hand — see PressureEventLogger.
    /// </summary>
    public sealed class PressureEventResult
    {
        public int EventId;
        public PressureEventType Type;
        public PlayerRole TargetRole;
        public float PlannedOffsetSeconds;
        public float ActualStartOffsetSeconds;

        public bool HadSupport;
        public float SupportResponseTimeSeconds; // meaningless if !HadSupport
        public ulong SupportClientId;

        public bool Recovered;
        public float RecoveryTimeSeconds; // meaningless if !Recovered
        public bool BurnedOrFailed;
    }
}
