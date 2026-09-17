namespace Teammate.Physio
{
    /// <summary>
    /// Four-state heart-rate classification from "Reading a Remote Teammate's Body",
    /// Table: Heart-rate and heart-rate-variability stimulus classification.
    /// Thresholds are always relative to a player's own seated baseline, never an
    /// absolute clinical scale.
    /// </summary>
    public enum HRState
    {
        HR1_RestingLowActivation = 0,
        HR2_FocusedActivation = 1,
        HR3_HighActivation = 2,
        HR4_OverdriveActivation = 3,
    }

    /// <summary>
    /// Four-state heart-rate-variability (RMSSD) classification from the same table.
    /// Expressed as a percentage of the player's own baseline RMSSD.
    /// </summary>
    public enum HRVState
    {
        V1_HighCoherence = 0,
        V2_StableVariability = 1,
        V3_ReducedVariability = 2,
        V4_SuppressedFragmented = 3,
    }

    /// <summary>
    /// The pair of classified states that is ever allowed to leave a client. Deliberately
    /// small and serializable as two bytes, matching the paper's design claim that
    /// "a viewing teammate's client never received the wearer's physiological time
    /// series directly ... only that player's current state."
    /// </summary>
    public readonly struct PhysioClassification
    {
        public readonly HRState Hr;
        public readonly HRVState Hrv;

        public PhysioClassification(HRState hr, HRVState hrv)
        {
            Hr = hr;
            Hrv = hrv;
        }

        public override string ToString() => $"{Hr}/{Hrv}";

        /// <summary>1-16 id for the icon condition grid (HR1V1=1 ... HR4V4=16), matching icon_state_id in the study logs.</summary>
        public int IconStateId => (int)Hr * 4 + (int)Hrv + 1;
    }

    public static class PhysioStateCodes
    {
        // Short codes used throughout the paper's tables/figures and in the study CSV logs
        // (hr_state / hrv_state / displayed_hr_state / displayed_hrv_state columns).
        public static string Code(HRState s) => s switch
        {
            HRState.HR1_RestingLowActivation => "HR1",
            HRState.HR2_FocusedActivation => "HR2",
            HRState.HR3_HighActivation => "HR3",
            HRState.HR4_OverdriveActivation => "HR4",
            _ => "HR?",
        };

        public static string Code(HRVState s) => s switch
        {
            HRVState.V1_HighCoherence => "V1",
            HRVState.V2_StableVariability => "V2",
            HRVState.V3_ReducedVariability => "V3",
            HRVState.V4_SuppressedFragmented => "V4",
            _ => "V?",
        };
    }
}
