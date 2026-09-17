using Unity.Netcode;
using UnityEngine;

namespace Teammate.Gameplay
{
    /// <summary>Raw ingredient in, prepped ingredient out. Never burns.</summary>
    [RequireComponent(typeof(NetworkObject))]
    public class ChoppingStation : TimedStationBase
    {
        protected override ItemKind InputKind => ItemKind.RawIngredient;
        protected override ItemKind ReadyOutputKind => ItemKind.PreppedIngredient;
        protected override ItemKind BurnedOutputKind => ItemKind.PreppedIngredient; // unreachable: burnAfterSeconds stays <= 0

        void Reset()
        {
            readyAfterSeconds = 3f;
            burnAfterSeconds = -1f; // chopping never burns
        }
    }
}
