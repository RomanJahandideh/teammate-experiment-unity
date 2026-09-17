using Teammate.Networking;

namespace Teammate.Mission
{
    /// <summary>The three scripted coordination probes from Study 2, each targeting one role and overloading it.</summary>
    public enum PressureEventType
    {
        IngredientBottleneck, // ~2:00, targets Prep — two active orders suddenly need the same ingredient
        PotOverload,          // ~4:30, targets Cook — two pots become active close together
        ServiceRush,          // late mission, targets Runner — dishes/plates/delivery pile up at once
    }

    /// <summary>Static schedule + target-role lookup, matching the paper's description exactly.</summary>
    public static class PressureEventCatalog
    {
        public static float PlannedOffsetSeconds(PressureEventType type) => type switch
        {
            PressureEventType.IngredientBottleneck => 120f, // 00:02:00
            PressureEventType.PotOverload => 270f,          // 00:04:30
            PressureEventType.ServiceRush => 390f,          // late phase of a 7-minute (420s) mission
            _ => 0f,
        };

        public static PlayerRole TargetRole(PressureEventType type) => type switch
        {
            PressureEventType.IngredientBottleneck => PlayerRole.Prep,
            PressureEventType.PotOverload => PlayerRole.Cook,
            PressureEventType.ServiceRush => PlayerRole.Runner,
            _ => PlayerRole.Unassigned,
        };

        public static string DisplayName(PressureEventType type) => type switch
        {
            PressureEventType.IngredientBottleneck => "Ingredient Bottleneck",
            PressureEventType.PotOverload => "Pot Overload",
            PressureEventType.ServiceRush => "Service Rush",
            _ => type.ToString(),
        };

        public static string TriggerDescription(PressureEventType type) => type switch
        {
            PressureEventType.IngredientBottleneck => "Two active orders require the same ingredient type",
            PressureEventType.PotOverload => "Two reactor pots become active close together",
            PressureEventType.ServiceRush => "Completed dishes, dirty plates, and delivery tasks pile up at once",
            _ => "",
        };

        /// <summary>Seconds allowed for a "support" action before it no longer counts as timely (Service Rush's 10s window; others just measure elapsed time).</summary>
        public static float SupportWindowSeconds(PressureEventType type) => type == PressureEventType.ServiceRush ? 10f : float.PositiveInfinity;
    }
}
