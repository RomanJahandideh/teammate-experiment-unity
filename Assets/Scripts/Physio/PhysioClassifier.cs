namespace Teammate.Physio
{
    /// <summary>
    /// Converts a raw HR (bpm) / HRV (RMSSD ms) sample into the four-state classification
    /// from the paper's stimulus table, always relative to the player's own baseline.
    ///
    /// HR: a state applies if the sample clears EITHER the absolute bpm delta OR the
    /// percent-above-baseline threshold for that state (the table gives both readings
    /// for the same boundary), so classification walks the states from most to least
    /// activated and returns the first one the sample qualifies for.
    ///
    /// HRV: purely a percentage of baseline RMSSD, per the table.
    /// </summary>
    public static class PhysioClassifier
    {
        public static HRState ClassifyHr(double currentBpm, double baselineBpm)
        {
            if (baselineBpm <= 0) return HRState.HR1_RestingLowActivation;

            double deltaBpm = currentBpm - baselineBpm;
            double pctAboveBaseline = deltaBpm / baselineBpm * 100.0;

            // HR4: Overdrive Activation — more than +30 bpm, or more than 45% above baseline.
            if (deltaBpm > 30.0 || pctAboveBaseline > 45.0)
                return HRState.HR4_OverdriveActivation;

            // HR3: High Activation — +16 to +30 bpm, or 26-45% above baseline.
            if (deltaBpm >= 16.0 || pctAboveBaseline >= 26.0)
                return HRState.HR3_HighActivation;

            // HR2: Focused Activation — +6 to +15 bpm, or 11-25% above baseline.
            if (deltaBpm >= 6.0 || pctAboveBaseline >= 11.0)
                return HRState.HR2_FocusedActivation;

            // HR1: Resting / Low Activation — baseline to +5 bpm, or <=10% above baseline.
            return HRState.HR1_RestingLowActivation;
        }

        public static HRVState ClassifyHrv(double currentRmssdMs, double baselineRmssdMs)
        {
            if (baselineRmssdMs <= 0) return HRVState.V2_StableVariability;

            double pctOfBaseline = currentRmssdMs / baselineRmssdMs * 100.0;

            if (pctOfBaseline >= 110.0) return HRVState.V1_HighCoherence;
            if (pctOfBaseline >= 90.0) return HRVState.V2_StableVariability;
            if (pctOfBaseline >= 60.0) return HRVState.V3_ReducedVariability;
            return HRVState.V4_SuppressedFragmented;
        }

        public static PhysioClassification Classify(
            double currentBpm, double baselineBpm,
            double currentRmssdMs, double baselineRmssdMs)
        {
            return new PhysioClassification(
                ClassifyHr(currentBpm, baselineBpm),
                ClassifyHrv(currentRmssdMs, baselineRmssdMs));
        }
    }
}
