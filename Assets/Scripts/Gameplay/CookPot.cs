using Unity.Netcode;
using UnityEngine;

namespace Teammate.Gameplay
{
    /// <summary>
    /// Prepped ingredient in, cooked ingredient out — or burned if left past
    /// burnAfterSeconds. This is the "reactor pot" the Pot Overload pressure event
    /// targets: MissionController activates a second pot's worth of urgency by shortening
    /// margin on two pots at once (see PressureEventScheduler).
    /// </summary>
    [RequireComponent(typeof(NetworkObject))]
    public class CookPot : TimedStationBase
    {
        protected override ItemKind InputKind => ItemKind.PreppedIngredient;
        protected override ItemKind ReadyOutputKind => ItemKind.CookedIngredient;
        protected override ItemKind BurnedOutputKind => ItemKind.BurnedIngredient;

        void Reset()
        {
            readyAfterSeconds = 6f;
            burnAfterSeconds = 12f;
        }
    }
}
